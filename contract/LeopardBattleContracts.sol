//SPDX-License-Identifier: MIT
pragma solidity ^0.8.20;

// Recommended dependency: @openzeppelin/contracts 5.x
import "@openzeppelin/contracts/token/ERC20/ERC20.sol";
import "@openzeppelin/contracts/token/ERC20/extensions/ERC20Burnable.sol";
import "@openzeppelin/contracts/token/ERC20/extensions/ERC20Capped.sol";
import "@openzeppelin/contracts/access/AccessControl.sol";
import "@openzeppelin/contracts/utils/ReentrancyGuard.sol";
import "@openzeppelin/contracts/token/ERC20/utils/SafeERC20.sol";

/**
 * @title TankCoin
 * @notice ERC-20 game currency for Leopard Battle.
 *         Maximum circulating supply: 1,000,000,000 TCOIN (18 decimals).
 *
 * The deployer receives DEFAULT_ADMIN_ROLE and MINTER_ROLE.
 * After deploying TankGame, the admin MUST grant:
 *   - MINTER_ROLE to the TankGame contract
 *   - GAME_ROLE to the TankGame contract
 *
 * Do not give minting roles to a Unity client or an ordinary player wallet.
 */
contract TankCoin is ERC20, ERC20Burnable, ERC20Capped, AccessControl {
    bytes32 public constant MINTER_ROLE = keccak256("MINTER_ROLE");
    bytes32 public constant GAME_ROLE = keccak256("GAME_ROLE");

    uint256 public constant MAX_SUPPLY = 1_000_000_000 ether;

    constructor() ERC20("Tank Coin", "TCOIN") ERC20Capped(MAX_SUPPLY) {
        _grantRole(DEFAULT_ADMIN_ROLE, msg.sender);
        _grantRole(MINTER_ROLE, msg.sender);
    }

    function mint(address to, uint256 amount) external onlyRole(MINTER_ROLE) {
        _mint(to, amount);
    }

    /// @dev Called by the authorized TankGame contract after a verified battle reward.
    function rewardPlayer(address player, uint256 amount) external onlyRole(GAME_ROLE) {
        require(player != address(0), "TankCoin: zero player");
        require(amount > 0, "TankCoin: zero reward");
        _mint(player, amount);
    }

    // Required by OpenZeppelin Contracts 5.x because ERC20Capped also overrides _update.
    function _update(address from, address to, uint256 value)
        internal
        override(ERC20, ERC20Capped)
    {
        super._update(from, to, value);
    }
}

/**
 * @title TankGame
 * @notice On-chain tanks, equipment, upgrades, TCOIN purchases, battle rewards,
 *         player-to-player equipment marketplace, and equipment transfers.
 *
 * TCOIN transfers between players use the normal ERC-20 transfer/transferFrom
 * functions on TankCoin. Marketplace buyers must approve this contract first.
 *
 * Important: the GAME_SERVER_ROLE must belong to a trusted backend signer,
 * never to a Unity client. The backend must independently verify battle results.
 */
contract TankGame is AccessControl, ReentrancyGuard {
    using SafeERC20 for IERC20;

    bytes32 public constant GAME_SERVER_ROLE = keccak256("GAME_SERVER_ROLE");

    TankCoin public immutable coin;
    IERC20 private immutable coinToken;

    // 1 ETH buys 1,000 TCOIN by default. Owner can change this rate.
    // Rate is whole TCOIN per 1 ETH; token amount is calculated in 18 decimals.
    uint256 public coinsPerEth = 1_000;

    struct Tank {
        uint16 hp;
        uint16 attack;
        uint16 defense;
        uint16 speed;
        uint16 level;
        uint32 upgradeCount;
        uint256 equipmentSlot1; // 0 = empty; slot 1 = weapon
        uint256 equipmentSlot2; // slot 2 = armor
        uint256 equipmentSlot3; // slot 3 = engine
    }

    mapping(address => Tank) public tanks;
    mapping(address => bool) public hasTank;

    struct Equipment {
        string name;
        uint8 equipmentType; // 1 = Weapon, 2 = Armor, 3 = Engine
        int16 bonusAttack;
        int16 bonusDefense;
        int16 bonusHp;
        int16 bonusSpeed;
        uint8 level;          // template/base level
        uint256 price;        // TCOIN, 18 decimals
        bool exists;
    }

    mapping(uint256 => Equipment) public equipments;
    uint256 public nextEquipmentId = 1;

    // Each player has their own equipment upgrade level. Upgrading one player's
    // item does NOT change the template or another player's equipment.
    mapping(address => mapping(uint256 => uint8)) public playerEquipmentLevel;
    mapping(address => mapping(uint256 => uint256)) public playerEquipment;

    struct Listing {
        address seller;
        uint256 equipmentId;
        uint256 amount;
        uint256 pricePerUnit; // TCOIN, 18 decimals
        bool active;
    }

    mapping(uint256 => Listing) public listings;
    uint256 public nextListingId = 1;

    // A battle ID can only be rewarded once, preventing accidental replay.
    mapping(bytes32 => bool) public processedBattleIds;

    event CoinPurchased(address indexed buyer, uint256 ethAmount, uint256 coinAmount);
    event CoinRateChanged(uint256 oldRate, uint256 newRate);
    event TankCreated(address indexed player);
    event EquipmentBought(address indexed player, uint256 indexed equipmentId, uint256 amount);
    event EquipmentUpgraded(address indexed player, uint256 indexed equipmentId, uint8 newLevel);
    event TankUpgraded(address indexed player, uint8 property, uint16 newValue, uint16 newTankLevel);
    event RewardEarned(address indexed player, uint256 amount, bytes32 indexed battleId, string reason);
    event EquipmentListed(uint256 indexed listingId, address indexed seller, uint256 indexed equipmentId, uint256 amount, uint256 pricePerUnit);
    event EquipmentSold(uint256 indexed listingId, address indexed seller, address indexed buyer, uint256 amount, uint256 totalPrice);
    event ListingCancelled(uint256 indexed listingId);
    event EquipmentEquipped(address indexed player, uint256 indexed equipmentId, uint8 slot);
    event EquipmentUnequipped(address indexed player, uint8 slot);
    event EquipmentTemplateCreated(uint256 indexed equipmentId, string name);
    event TreasuryCoinsWithdrawn(address indexed to, uint256 amount);
    event TreasuryETHWithdrawn(address indexed to, uint256 amount);

    constructor(address coinAddress) {
        require(coinAddress != address(0), "TankGame: zero coin address");
        coin = TankCoin(coinAddress);
        coinToken = IERC20(coinAddress);

        _grantRole(DEFAULT_ADMIN_ROLE, msg.sender);
        _grantRole(GAME_SERVER_ROLE, msg.sender);

        // Preserve the original five starter equipment templates.
        _createEquipment("Basic Cannon", 1, 15, 0, 0, 0, 1, 100 ether);
        _createEquipment("Steel Armor", 2, 0, 12, 30, 0, 1, 120 ether);
        _createEquipment("Turbo Engine", 3, 0, 0, 0, 10, 1, 90 ether);
        _createEquipment("Heavy Cannon", 1, 35, 0, 0, -5, 1, 300 ether);
        _createEquipment("Reactive Armor", 2, 0, 25, 50, -3, 1, 350 ether);
    }

    // ---------------------------------------------------------------------
    // 1. BUY TCOIN WITH ETH
    // ---------------------------------------------------------------------

    function purchaseCoin() external payable nonReentrant {
        require(msg.value > 0, "TankGame: send ETH");
        uint256 amount = msg.value * coinsPerEth;
        require(amount > 0, "TankGame: zero amount");

        // TankGame must have MINTER_ROLE on TankCoin.
        coin.mint(msg.sender, amount);
        emit CoinPurchased(msg.sender, msg.value, amount);
    }

    function setCoinsPerEth(uint256 newRate) external onlyRole(DEFAULT_ADMIN_ROLE) {
        require(newRate > 0 && newRate <= 1_000_000, "TankGame: invalid rate");
        uint256 oldRate = coinsPerEth;
        coinsPerEth = newRate;
        emit CoinRateChanged(oldRate, newRate);
    }

    // ---------------------------------------------------------------------
    // 2. CREATE PLAYER TANK
    // ---------------------------------------------------------------------

    function createTank() external {
        require(!hasTank[msg.sender], "TankGame: tank already exists");

        tanks[msg.sender] = Tank({
            hp: 100,
            attack: 20,
            defense: 15,
            speed: 12,
            level: 1,
            upgradeCount: 0,
            equipmentSlot1: 0,
            equipmentSlot2: 0,
            equipmentSlot3: 0
        });

        hasTank[msg.sender] = true;
        emit TankCreated(msg.sender);
    }

    // ---------------------------------------------------------------------
    // 3. BUY EQUIPMENT
    // ---------------------------------------------------------------------

    function buyEquipment(uint256 equipmentId, uint256 amount) external nonReentrant {
        require(amount > 0, "TankGame: zero amount");
        Equipment storage eq = equipments[equipmentId];
        require(eq.exists, "TankGame: equipment missing");

        uint256 totalCost = eq.price * amount;
        coinToken.safeTransferFrom(msg.sender, address(this), totalCost);

        playerEquipment[msg.sender][equipmentId] += amount;
        if (playerEquipmentLevel[msg.sender][equipmentId] == 0) {
            playerEquipmentLevel[msg.sender][equipmentId] = eq.level;
        }

        emit EquipmentBought(msg.sender, equipmentId, amount);
    }

    // ---------------------------------------------------------------------
    // 4. UPGRADE A PLAYER'S EQUIPMENT
    // ---------------------------------------------------------------------

    function upgradeEquipment(uint256 equipmentId) external nonReentrant {
        require(playerEquipment[msg.sender][equipmentId] > 0, "TankGame: item not owned");

        Equipment storage eq = equipments[equipmentId];
        require(eq.exists, "TankGame: equipment missing");

        uint8 currentLevel = playerEquipmentLevel[msg.sender][equipmentId];
        if (currentLevel == 0) currentLevel = eq.level;
        require(currentLevel < 50, "TankGame: max equipment level");

        uint256 upgradeCost = eq.price * uint256(currentLevel);
        coinToken.safeTransferFrom(msg.sender, address(this), upgradeCost);

        playerEquipmentLevel[msg.sender][equipmentId] = currentLevel + 1;
        emit EquipmentUpgraded(msg.sender, equipmentId, currentLevel + 1);
    }

    /// @notice Returns this player's equipment bonuses after their personal upgrades.
    function getPlayerEquipmentStats(address player, uint256 equipmentId)
        public
        view
        returns (int256 attack, int256 defense, int256 hp, int256 speed, uint8 level)
    {
        Equipment storage eq = equipments[equipmentId];
        require(eq.exists, "TankGame: equipment missing");

        level = playerEquipmentLevel[player][equipmentId];
        if (level == 0) level = eq.level;

        uint256 multiplier = 100 + (uint256(level) - uint256(eq.level)) * 20;
        attack = int256(eq.bonusAttack) * int256(multiplier) / 100;
        defense = int256(eq.bonusDefense) * int256(multiplier) / 100;
        hp = int256(eq.bonusHp) * int256(multiplier) / 100;
        speed = int256(eq.bonusSpeed) * int256(multiplier) / 100;
    }

    // ---------------------------------------------------------------------
    // 5. UPGRADE TANK PROPERTY
    // ---------------------------------------------------------------------

    function upgradeTankProperty(uint8 property) external nonReentrant {
        // property: 1 = HP, 2 = Attack, 3 = Defense, 4 = Speed
        require(hasTank[msg.sender], "TankGame: no tank");
        require(property >= 1 && property <= 4, "TankGame: invalid property");

        Tank storage tank = tanks[msg.sender];
        uint256 cost = 50 ether * uint256(tank.level);
        coinToken.safeTransferFrom(msg.sender, address(this), cost);

        uint16 newValue;
        if (property == 1) {
            require(tank.hp <= type(uint16).max - 15, "TankGame: HP limit");
            tank.hp += 15;
            newValue = tank.hp;
        } else if (property == 2) {
            require(tank.attack <= type(uint16).max - 5, "TankGame: attack limit");
            tank.attack += 5;
            newValue = tank.attack;
        } else if (property == 3) {
            require(tank.defense <= type(uint16).max - 4, "TankGame: defense limit");
            tank.defense += 4;
            newValue = tank.defense;
        } else {
            require(tank.speed <= type(uint16).max - 3, "TankGame: speed limit");
            tank.speed += 3;
            newValue = tank.speed;
        }

        tank.upgradeCount += 1;
        // Predictable progression: every five successful property upgrades.
        if (tank.upgradeCount % 5 == 0 && tank.level < type(uint16).max) {
            tank.level += 1;
        }

        emit TankUpgraded(msg.sender, property, newValue, tank.level);
    }

    // ---------------------------------------------------------------------
    // 6. ON-CHAIN BATTLE REWARDS
    // ---------------------------------------------------------------------

    function rewardDestroyEnemy(
        address player,
        uint256 amount,
        string calldata reason,
        bytes32 battleId
    ) external onlyRole(GAME_SERVER_ROLE) {
        require(player != address(0), "TankGame: zero player");
        require(amount > 0, "TankGame: zero reward");
        require(battleId != bytes32(0), "TankGame: empty battle ID");
        require(!processedBattleIds[battleId], "TankGame: battle already rewarded");

        processedBattleIds[battleId] = true;

        // TankGame must have GAME_ROLE on TankCoin.
        coin.rewardPlayer(player, amount);
        emit RewardEarned(player, amount, battleId, reason);
    }

    // ---------------------------------------------------------------------
    // 7. PLAYER EQUIPMENT MARKETPLACE
    // ---------------------------------------------------------------------

    function listEquipment(uint256 equipmentId, uint256 amount, uint256 pricePerUnit)
        external
    {
        require(equipments[equipmentId].exists, "TankGame: equipment missing");
        require(amount > 0, "TankGame: zero amount");
        require(playerEquipment[msg.sender][equipmentId] >= amount, "TankGame: not enough items");
        require(pricePerUnit > 0, "TankGame: zero price");

        // Lock listed units so they cannot be sold or listed twice.
        playerEquipment[msg.sender][equipmentId] -= amount;

        uint256 listingId = nextListingId++;
        listings[listingId] = Listing({
            seller: msg.sender,
            equipmentId: equipmentId,
            amount: amount,
            pricePerUnit: pricePerUnit,
            active: true
        });

        emit EquipmentListed(listingId, msg.sender, equipmentId, amount, pricePerUnit);
    }

    function buyListedEquipment(uint256 listingId, uint256 amount) external nonReentrant {
        Listing storage listing = listings[listingId];
        require(listing.active, "TankGame: listing inactive");
        require(listing.seller != msg.sender, "TankGame: cannot buy own listing");
        require(amount > 0 && amount <= listing.amount, "TankGame: invalid amount");

        uint256 totalPrice = listing.pricePerUnit * amount;
        address seller = listing.seller;
        uint256 equipmentId = listing.equipmentId;

        // Buyer must first call TCOIN.approve(TankGameAddress, totalPrice).
        coinToken.safeTransferFrom(msg.sender, seller, totalPrice);

        listing.amount -= amount;
        if (listing.amount == 0) listing.active = false;

        playerEquipment[msg.sender][equipmentId] += amount;
        if (playerEquipmentLevel[msg.sender][equipmentId] == 0) {
            playerEquipmentLevel[msg.sender][equipmentId] = equipments[equipmentId].level;
        }

        emit EquipmentSold(listingId, seller, msg.sender, amount, totalPrice);
    }

    function cancelListing(uint256 listingId) external {
        Listing storage listing = listings[listingId];
        require(listing.seller == msg.sender, "TankGame: not your listing");
        require(listing.active, "TankGame: listing inactive");

        uint256 remaining = listing.amount;
        uint256 equipmentId = listing.equipmentId;
        listing.amount = 0;
        listing.active = false;

        playerEquipment[msg.sender][equipmentId] += remaining;
        emit ListingCancelled(listingId);
    }

    // ---------------------------------------------------------------------
    // 8. EQUIP / UNEQUIP ITEMS
    // ---------------------------------------------------------------------

    function equipItem(uint256 equipmentId, uint8 slot) external {
        require(hasTank[msg.sender], "TankGame: no tank");
        require(equipments[equipmentId].exists, "TankGame: equipment missing");
        require(playerEquipment[msg.sender][equipmentId] > 0, "TankGame: item not owned");
        require(slot >= 1 && slot <= 3, "TankGame: invalid slot");
        require(equipments[equipmentId].equipmentType == slot, "TankGame: wrong equipment slot");

        Tank storage tank = tanks[msg.sender];
        if (slot == 1) tank.equipmentSlot1 = equipmentId;
        else if (slot == 2) tank.equipmentSlot2 = equipmentId;
        else tank.equipmentSlot3 = equipmentId;

        emit EquipmentEquipped(msg.sender, equipmentId, slot);
    }

    function unequipItem(uint8 slot) external {
        require(hasTank[msg.sender], "TankGame: no tank");
        require(slot >= 1 && slot <= 3, "TankGame: invalid slot");

        Tank storage tank = tanks[msg.sender];
        if (slot == 1) tank.equipmentSlot1 = 0;
        else if (slot == 2) tank.equipmentSlot2 = 0;
        else tank.equipmentSlot3 = 0;

        emit EquipmentUnequipped(msg.sender, slot);
    }

    /// @notice Tank base stats plus the bonuses/penalties of equipped items.
    function getTankEffectiveStats(address player)
        external
        view
        returns (int256 hp, int256 attack, int256 defense, int256 speed)
    {
        require(hasTank[player], "TankGame: no tank");
        Tank storage tank = tanks[player];

        hp = int256(uint256(tank.hp));
        attack = int256(uint256(tank.attack));
        defense = int256(uint256(tank.defense));
        speed = int256(uint256(tank.speed));

        uint256[3] memory ids = [
            tank.equipmentSlot1,
            tank.equipmentSlot2,
            tank.equipmentSlot3
        ];

        for (uint256 i = 0; i < 3; i++) {
            if (ids[i] != 0) {
                (int256 a, int256 d, int256 h, int256 s,) =
                    getPlayerEquipmentStats(player, ids[i]);
                attack += a;
                defense += d;
                hp += h;
                speed += s;
            }
        }
    }

    // ---------------------------------------------------------------------
    // 9. ADMIN: EQUIPMENT TEMPLATES AND TREASURY
    // ---------------------------------------------------------------------

    function createNewEquipment(
        string calldata name,
        uint8 equipmentType,
        int16 atk,
        int16 def,
        int16 hp,
        int16 spd,
        uint256 price
    ) external onlyRole(DEFAULT_ADMIN_ROLE) {
        require(equipmentType >= 1 && equipmentType <= 3, "TankGame: invalid equipment type");
        require(bytes(name).length > 0, "TankGame: empty name");
        require(price > 0, "TankGame: zero price");

        _createEquipment(name, equipmentType, atk, def, hp, spd, 1, price);
    }

    function _createEquipment(
        string memory name,
        uint8 equipmentType,
        int16 atk,
        int16 def,
        int16 hp,
        int16 spd,
        uint8 level,
        uint256 price
    ) internal {
        require(equipmentType >= 1 && equipmentType <= 3, "TankGame: invalid equipment type");

        uint256 id = nextEquipmentId++;
        equipments[id] = Equipment({
            name: name,
            equipmentType: equipmentType,
            bonusAttack: atk,
            bonusDefense: def,
            bonusHp: hp,
            bonusSpeed: spd,
            level: level,
            price: price,
            exists: true
        });

        emit EquipmentTemplateCreated(id, name);
    }

    function withdrawETH(address payable to, uint256 amount)
        external
        onlyRole(DEFAULT_ADMIN_ROLE)
        nonReentrant
    {
        require(to != address(0), "TankGame: zero recipient");
        require(amount <= address(this).balance, "TankGame: insufficient ETH");

        (bool success,) = to.call{value: amount}("");
        require(success, "TankGame: ETH withdrawal failed");
        emit TreasuryETHWithdrawn(to, amount);
    }

    function withdrawCoins(address to, uint256 amount)
        external
        onlyRole(DEFAULT_ADMIN_ROLE)
        nonReentrant
    {
        require(to != address(0), "TankGame: zero recipient");
        coinToken.safeTransfer(to, amount);
        emit TreasuryCoinsWithdrawn(to, amount);
    }

    // ---------------------------------------------------------------------
    // 10. VIEW HELPERS
    // ---------------------------------------------------------------------

    function getPlayerTank(address player) external view returns (Tank memory) {
        return tanks[player];
    }

    function getPlayerEquipmentBalance(address player, uint256 equipmentId)
        external
        view
        returns (uint256)
    {
        return playerEquipment[player][equipmentId];
    }

    receive() external payable {
        revert("TankGame: use purchaseCoin()");
    }
}
