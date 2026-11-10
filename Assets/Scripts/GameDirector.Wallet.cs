using System;
using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Reown.AppKit.Unity;
using Reown.AppKit.Unity.Model;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public partial class GameDirector
{
    private const string EthereumSepoliaRpcUrl =
        "https://ethereum-sepolia-rpc.publicnode.com";
    private const string EthereumSepoliaFallbackRpcUrl =
        "https://1rpc.io/sepolia";
    private const string TcoinAbi =
        "function balanceOf(address account) view returns (uint256)\n" +
        "function transfer(address to, uint256 amount) returns (bool)\n" +
        "function approve(address spender, uint256 amount) returns (bool)";
    private const string TankGameAbi =
        "function purchaseCoin() payable\n" +
        "function buyEquipment(uint256 equipmentId, uint256 amount)\n" +
        "function upgradeEquipment(uint256 equipmentId)\n" +
        "function equipItem(uint256 equipmentId, uint8 slot)\n" +
        "function unequipItem(uint8 slot)\n" +
        "function listEquipment(uint256 equipmentId, uint256 amount, uint256 pricePerUnit)\n" +
        "function buyListedEquipment(uint256 listingId, uint256 amount)\n" +
        "function cancelListing(uint256 listingId)\n" +
        "function getPlayerEquipmentBalance(address player, uint256 equipmentId) view returns (uint256)\n" +
        "function playerEquipmentLevel(address player, uint256 equipmentId) view returns (uint8)";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void AbramsWalletConnect(string gameObjectName);

    [DllImport("__Internal")]
    private static extern void AbramsWalletSignMessage(
        string gameObjectName,
        int requestId,
        string address,
        string message
    );

    [DllImport("__Internal")]
    private static extern void AbramsWalletReadContract(
        string gameObjectName,
        int requestId,
        string address,
        string data
    );

    [DllImport("__Internal")]
    private static extern void AbramsWalletSendTransaction(
        string gameObjectName,
        int requestId,
        string address,
        string data,
        string value,
        string gas
    );
#endif

    [Serializable]
    private sealed class BrowserWalletResponse
    {
        public int requestId;
        public string address = "";
        public string chainId = "";
        public string result = "";
        public string error = "";
    }

    [Serializable]
    private sealed class JsonRpcResponse
    {
        public string result;
        public JsonRpcError error;
    }

    [Serializable]
    private sealed class JsonRpcError
    {
        public int code;
        public string message;
    }

    private bool walletKitReady;
    private bool walletContractsReady;
    private bool browserWalletConnected;
    private string browserWalletAddress = "";
    private string browserWalletChainId = "";
    private int nextBrowserWalletRequestId;
    private readonly System.Collections.Generic.Dictionary<
        int,
        TaskCompletionSource<string>
    > browserWalletRequests =
        new System.Collections.Generic.Dictionary<
            int,
            TaskCompletionSource<string>
        >();
    private bool onlineWalletActionInProgress;
    private bool walletLinkInProgress;
    private bool connectedWalletOnEthereumSepolia;
    private string walletAccountTokenProcessed = "";
    private string connectedWalletAddress = "";
    private string walletEconomyStatus = "Connecting wallet service...";
    private string tcoinBalanceLabel = "Not connected";
    private string walletRpcError = "";
    private string ethPurchaseAmountInput = "0.01";
    private string tcoinTransferAddressInput = "";
    private string tcoinTransferAmountInput = "1";
    private string equipmentIdInput = "1";
    private string equipmentAmountInput = "1";
    private string equipmentApprovalAmountInput = "100";
    private string equipmentSlotInput = "1";
    private string marketplacePriceInput = "100";
    private string marketplaceListingIdInput = "1";
    private string marketplaceBuyAmountInput = "1";
    private UnityEngine.Vector2 walletEconomyScroll;
    private BigInteger[] equipmentInventory = new BigInteger[5];
    private byte[] equipmentLevels = new byte[5];

    private void StartWalletIntegration()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        walletKitReady = true;
        walletEconomyStatus =
            "Use the MetaMask extension button to connect your browser wallet.";
#else
        if (reownAppKitPrefab == null)
        {
            walletEconomyStatus =
                "Reown AppKit prefab is not assigned to GameDirector.";
            Debug.LogError(walletEconomyStatus, this);
            return;
        }

        if (AppKit.Instance == null)
            Instantiate(reownAppKitPrefab);

        StartCoroutine(InitializeWalletKit());
#endif
    }

    private IEnumerator InitializeWalletKit()
    {
        Task initializeTask = null;
        try
        {
            if (!AppKit.IsInitialized)
            {
                initializeTask = AppKit.InitializeAsync(new AppKitConfig
                {
                    projectId = reownProjectId,
                    metadata = new Metadata(
                        "Abrams Tank Battle",
                        "Online tank battles and TCOIN equipment",
                        "https://reown.com",
                        "https://raw.githubusercontent.com/reown-com/reown-dotnet/develop/sample/Reown.AppKit.Unity/Assets/Textures/appkit-icon-unity.png"
                    ),
                    supportedChains = new[]
                    {
                        new Chain(
                            ChainConstants.Namespaces.Evm,
                            "11155111",
                            "Ethereum Sepolia",
                            new Currency("Ether", "ETH", 18),
                            new BlockExplorer(
                                "Etherscan",
                                "https://sepolia.etherscan.io"
                            ),
                            EthereumSepoliaRpcUrl,
                            true,
                            "https://api.web3modal.com/public/getAssetImage/7289c336-3981-4081-c5f4-efc26ac64a00"
                        )
                    }
                });
            }
        }
        catch (Exception exception)
        {
            walletEconomyStatus =
                "Wallet initialization failed: " + exception.Message;
            Debug.LogException(exception, this);
            yield break;
        }

        while (initializeTask != null && !initializeTask.IsCompleted)
            yield return null;

        if (initializeTask != null && initializeTask.IsFaulted)
        {
            Exception exception = initializeTask.Exception;
            walletEconomyStatus =
                "Wallet initialization failed: " +
                (exception == null ? "unknown error" : exception.GetBaseException().Message);
            Debug.LogException(exception, this);
            yield break;
        }

        walletKitReady = true;
        Task<bool> resumeTask = null;
        try
        {
            resumeTask =
                AppKit.ConnectorController.TryResumeSessionAsync();
        }
        catch (Exception exception)
        {
            walletEconomyStatus =
                "Could not restore the wallet session: " + exception.Message;
            Debug.LogException(exception, this);
            yield break;
        }

        while (resumeTask != null && !resumeTask.IsCompleted)
            yield return null;

        if (resumeTask != null && resumeTask.IsFaulted)
        {
            Exception exception = resumeTask.Exception;
            walletEconomyStatus =
                "Could not restore the wallet session: " +
                (exception == null ? "unknown error" : exception.GetBaseException().Message);
            Debug.LogException(exception, this);
            yield break;
        }

        walletEconomyStatus =
            "Wallet service ready. Connect an Ethereum Sepolia wallet.";
        if (resumeTask != null && resumeTask.Status == TaskStatus.RanToCompletion &&
            resumeTask.Result)
            SyncConnectedWallet();
    }

    private void SyncConnectedWallet()
    {
        if (!walletKitReady || !IsWalletConnected())
        {
            if (connectedWalletAddress.Length > 0)
            {
                connectedWalletAddress = "";
                connectedWalletOnEthereumSepolia = false;
                walletContractsReady = false;
                tcoinBalanceLabel = "Not connected";
                walletEconomyStatus = "Wallet disconnected.";
                walletAccountTokenProcessed = "";
            }
            return;
        }

        string address = GetConnectedWalletAddress();
        if (string.Equals(
                connectedWalletAddress,
                address,
                StringComparison.OrdinalIgnoreCase))
        {
            bool nowOnEthereumSepolia = IsWalletOnEthereumSepolia();
            if (!nowOnEthereumSepolia)
            {
                connectedWalletOnEthereumSepolia = false;
                walletEconomyStatus =
                    "Switch your wallet to Ethereum Sepolia to continue.";
                return;
            }
            if (!connectedWalletOnEthereumSepolia)
            {
                connectedWalletOnEthereumSepolia = true;
                HandleWalletOnEthereumSepolia();
            }
            else if (!string.IsNullOrEmpty(onlineAuthToken) &&
                     walletAccountTokenProcessed != onlineAuthToken)
            {
                HandleWalletOnEthereumSepolia();
            }
            return;
        }

        connectedWalletAddress = address;
        walletAccountTokenProcessed = "";
        connectedWalletOnEthereumSepolia = IsWalletOnEthereumSepolia();
        walletContractsReady = false;
        tcoinBalanceLabel = "Checking Ethereum Sepolia contracts...";
        if (!connectedWalletOnEthereumSepolia)
        {
            walletEconomyStatus =
                "Switch your wallet to Ethereum Sepolia to continue.";
            return;
        }
        HandleWalletOnEthereumSepolia();
    }

    public void OnBrowserWalletStateChanged(string json)
    {
        BrowserWalletResponse response =
            JsonUtility.FromJson<BrowserWalletResponse>(json);
        if (response == null)
        {
            walletEconomyStatus =
                "The browser wallet returned an invalid account response.";
            return;
        }

        browserWalletAddress = response.address ?? "";
        browserWalletChainId = response.chainId ?? "";
        browserWalletConnected =
            !string.IsNullOrWhiteSpace(browserWalletAddress);
        if (!string.IsNullOrEmpty(response.error))
        {
            walletEconomyStatus = response.error;
            if (showOnlineMenu)
                onlineStatusMessage = response.error;
            if (!browserWalletConnected)
                return;
        }
        SyncConnectedWallet();
    }

    public void OnBrowserWalletRequestCompleted(string json)
    {
        BrowserWalletResponse response =
            JsonUtility.FromJson<BrowserWalletResponse>(json);
        if (response == null ||
            !browserWalletRequests.TryGetValue(
                response.requestId,
                out TaskCompletionSource<string> completion))
        {
            Debug.LogWarning(
                "Received an unexpected browser wallet response.",
                this
            );
            return;
        }

        browserWalletRequests.Remove(response.requestId);
        if (!string.IsNullOrEmpty(response.error))
            completion.TrySetException(
                new InvalidOperationException(response.error)
            );
        else
            completion.TrySetResult(response.result ?? "");
    }

    private string GetConnectedWalletAddress()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return browserWalletConnected ? browserWalletAddress : "";
#else
        return AppKit.AccountController.Address;
#endif
    }

    private void HandleWalletOnEthereumSepolia()
    {
        walletEconomyStatus = "Wallet connected: " + connectedWalletAddress;
        if (showOnlineMenu)
            onlineStatusMessage =
                "MetaMask connected. Preparing wallet verification...";
        StartCoroutine(CheckConfiguredContracts());

        if (string.IsNullOrEmpty(onlineAuthToken))
        {
            walletEconomyStatus += " Sign in to link this wallet to your profile.";
            if (showOnlineMenu)
                onlineStatusMessage = walletEconomyStatus;
            return;
        }

        if (walletAccountTokenProcessed == onlineAuthToken)
            return;
        walletAccountTokenProcessed = onlineAuthToken;

        if (!string.IsNullOrEmpty(onlineWalletAddress))
        {
            if (!string.Equals(
                    onlineWalletAddress,
                    connectedWalletAddress,
                    StringComparison.OrdinalIgnoreCase))
                walletEconomyStatus =
                    "This account is linked to a different wallet address.";
            else
                StartCoroutine(RefreshWalletEconomy());
            return;
        }

        if (!walletLinkInProgress)
            StartCoroutine(LinkConnectedWallet());
    }

    private void ConnectOnlineWallet()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!walletKitReady)
        {
            walletEconomyStatus = "Browser wallet integration is not ready.";
            return;
        }
        try
        {
            AbramsWalletConnect(gameObject.name);
            walletEconomyStatus =
                "Approve the MetaMask connection and Ethereum Sepolia switch in Chrome.";
        }
        catch (Exception exception)
        {
            walletEconomyStatus =
                "Could not connect MetaMask: " + exception.Message;
            Debug.LogException(exception, this);
        }
#else
        if (!walletKitReady)
        {
            walletEconomyStatus =
                "Wallet service is not ready. " +
                "Check the Reown AppKit prefab and Project ID.";
            return;
        }

        try
        {
            AppKit.OpenModal();
        }
        catch (Exception exception)
        {
            walletEconomyStatus =
                "Could not open the wallet selector: " + exception.Message;
            Debug.LogException(exception, this);
        }
#endif
    }

    private void OpenMetaMaskWalletSearch()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ConnectOnlineWallet();
#else
        if (!walletKitReady)
        {
            walletEconomyStatus =
                "Wallet service is not ready. Check the Reown AppKit prefab.";
            return;
        }

        try
        {
            AppKit.OpenModal(ViewType.WalletSearch);
        }
        catch (Exception exception)
        {
            walletEconomyStatus =
                "Could not open wallet search: " + exception.Message;
            Debug.LogException(exception, this);
        }
#endif
    }

    private bool IsReownWalletModalOpen()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return false;
#else
        return walletKitReady && AppKit.IsModalOpen;
#endif
    }

    private IEnumerator LinkConnectedWallet()
    {
        if (walletLinkInProgress || string.IsNullOrEmpty(onlineAuthToken) ||
            string.IsNullOrEmpty(connectedWalletAddress))
            yield break;

        walletLinkInProgress = true;
        if (showOnlineMenu)
            onlineStatusMessage = "Requesting wallet-link signature...";
        OnlineWalletChallengeResponse challenge = null;
        string requestError = "";
        onlineRequestInProgress = true;
        yield return SendOnlineRequest(
            "/api/wallet/challenge",
            "POST",
            JsonUtility.ToJson(new OnlineRequestData
            {
                address = connectedWalletAddress
            }),
            false,
            body => challenge =
                JsonUtility.FromJson<OnlineWalletChallengeResponse>(body),
            error => requestError = error
        );
        onlineRequestInProgress = false;

        if (!string.IsNullOrEmpty(requestError) || challenge == null ||
            string.IsNullOrEmpty(challenge.message) ||
            string.IsNullOrEmpty(challenge.nonce))
        {
            walletLinkInProgress = false;
            walletEconomyStatus = string.IsNullOrEmpty(requestError)
                ? "The server returned an invalid wallet challenge."
                : "Could not request a wallet challenge: " + requestError;
            if (showOnlineMenu)
                onlineStatusMessage = walletEconomyStatus;
            yield break;
        }

        Task<string> signatureTask = null;
        try
        {
            signatureTask = SignWalletMessage(challenge.message);
        }
        catch (Exception exception)
        {
            walletLinkInProgress = false;
            walletEconomyStatus =
                "Wallet signing failed: " + exception.Message;
            if (showOnlineMenu)
                onlineStatusMessage = walletEconomyStatus;
            Debug.LogException(exception, this);
            yield break;
        }

        while (!signatureTask.IsCompleted)
            yield return null;
        if (signatureTask.IsFaulted || signatureTask.IsCanceled)
        {
            Exception exception = signatureTask.Exception;
            walletLinkInProgress = false;
            walletEconomyStatus = signatureTask.IsCanceled
                ? "Wallet signature was cancelled."
                : "Wallet signing failed: " +
                    (exception == null ? "unknown error" : exception.GetBaseException().Message);
            if (exception != null)
                Debug.LogException(exception, this);
            if (showOnlineMenu)
                onlineStatusMessage = walletEconomyStatus;
            yield break;
        }

        OnlineWalletResponse linkedWallet = null;
        requestError = "";
        onlineRequestInProgress = true;
        yield return SendOnlineRequest(
            "/api/wallet/verify",
            "POST",
            JsonUtility.ToJson(new OnlineRequestData
            {
                address = connectedWalletAddress,
                nonce = challenge.nonce,
                signature = signatureTask.Result
            }),
            false,
            body => linkedWallet =
                JsonUtility.FromJson<OnlineWalletResponse>(body),
            error => requestError = error
        );
        onlineRequestInProgress = false;
        walletLinkInProgress = false;

        if (!string.IsNullOrEmpty(requestError) || linkedWallet == null ||
            string.IsNullOrEmpty(linkedWallet.walletAddress))
        {
            walletEconomyStatus = string.IsNullOrEmpty(requestError)
                ? "The server returned an invalid wallet-link response."
                : "Could not link wallet: " + requestError;
            if (showOnlineMenu)
                onlineStatusMessage = walletEconomyStatus;
            yield break;
        }

        onlineWalletAddress = linkedWallet.walletAddress;
        walletEconomyStatus = "Wallet verified and linked to your account.";
        if (showOnlineMenu)
            onlineStatusMessage = walletEconomyStatus;
        StartCoroutine(RefreshOnlineProfile());
        if (walletContractsReady)
            StartCoroutine(RefreshWalletEconomy());
    }

    private Task<string> SignWalletMessage(string message)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return SendBrowserWalletRequest(
            requestId => AbramsWalletSignMessage(
                gameObject.name,
                requestId,
                browserWalletAddress,
                message
            )
        );
#else
        return AppKit.Evm.SignMessageAsync(message);
#endif
    }

    private Task<string> SendBrowserWalletRequest(Action<int> beginRequest)
    {
        int requestId = ++nextBrowserWalletRequestId;
        TaskCompletionSource<string> completion =
            new TaskCompletionSource<string>();
        browserWalletRequests.Add(requestId, completion);
        try
        {
            beginRequest(requestId);
        }
        catch (Exception exception)
        {
            browserWalletRequests.Remove(requestId);
            completion.TrySetException(exception);
        }
        return completion.Task;
    }

    private IEnumerator CheckConfiguredContracts()
    {
        walletContractsReady = false;
        walletRpcError = "";
        yield return CheckContractCode(tankCoinContractAddress);
        string coinCode = walletRpcError;
        walletRpcError = "";
        yield return CheckContractCode(tankGameContractAddress);
        string gameCode = walletRpcError;

        if (coinCode == "deployed" && gameCode == "deployed")
        {
            walletContractsReady = true;
            walletEconomyStatus =
                "TCOIN contracts verified on Ethereum Sepolia.";
            if (string.Equals(
                    connectedWalletAddress,
                    onlineWalletAddress,
                    StringComparison.OrdinalIgnoreCase))
                StartCoroutine(RefreshWalletEconomy());
            yield break;
        }

        string problem = coinCode != "deployed"
            ? "TankCoin " + coinCode
            : "TankGame " + gameCode;
        if (problem.Contains("RPC unavailable:"))
        {
            walletEconomyStatus =
                "Could not verify contracts on Ethereum Sepolia: " + problem +
                ". Economy actions remain disabled.";
            tcoinBalanceLabel = "Contract check failed";
        }
        else
        {
            walletEconomyStatus =
                problem + ". Deploy both contracts on Ethereum Sepolia and update " +
                "the configured addresses to enable the economy.";
            tcoinBalanceLabel = "Contracts not deployed";
        }
        Debug.LogWarning(walletEconomyStatus, this);
    }

    private IEnumerator CheckContractCode(string address)
    {
        if (!IsWalletAddress(address))
        {
            walletRpcError = "invalid contract address";
            yield break;
        }

        string payload =
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"eth_getCode\"," +
            "\"params\":[\"" + address + "\",\"latest\"]}";
        string[] rpcUrls =
        {
            EthereumSepoliaRpcUrl,
            EthereumSepoliaFallbackRpcUrl
        };
        string lastRpcError = "";
        for (int i = 0; i < rpcUrls.Length; i++)
        {
            using (UnityWebRequest request = new UnityWebRequest(
                rpcUrls[i],
                "POST"
            ))
            {
                request.uploadHandler =
                    new UploadHandlerRaw(
                        System.Text.Encoding.UTF8.GetBytes(payload)
                    );
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    lastRpcError = "HTTP " + (long)request.responseCode +
                        " from " + rpcUrls[i];
                    continue;
                }

                JsonRpcResponse response =
                    JsonUtility.FromJson<JsonRpcResponse>(
                        request.downloadHandler.text
                    );
                if (response == null)
                {
                    lastRpcError = "invalid response from " + rpcUrls[i];
                    continue;
                }
                if (response.error != null)
                {
                    lastRpcError = "RPC error from " + rpcUrls[i] +
                        (string.IsNullOrWhiteSpace(response.error.message)
                            ? " (code " + response.error.code + ")"
                            : ": " + response.error.message);
                    continue;
                }
                if (string.IsNullOrEmpty(response.result))
                {
                    lastRpcError = "missing contract-code result from " +
                        rpcUrls[i];
                    continue;
                }

                walletRpcError = response.result != "0x" &&
                    response.result != "0x0"
                    ? "deployed"
                    : "has no bytecode at " + address;
                yield break;
            }
        }

        walletRpcError = "RPC unavailable: " + lastRpcError;
    }

    private Task<BigInteger> ReadWalletUInt256(
        string contractAddress,
        string function,
        params object[] arguments)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        string selector;
        if (function == "balanceOf")
            selector = "0x70a08231";
        else if (function == "getPlayerEquipmentBalance")
            selector = "0xd3ef1c61";
        else if (function == "playerEquipmentLevel")
            selector = "0x0de00b63";
        else
            throw new ArgumentException(
                "Unsupported wallet contract read: " + function,
                nameof(function)
            );

        string data = EncodeContractCall(selector, arguments);
        return ReadBrowserUInt256(contractAddress, data);
#else
        if (function == "balanceOf")
            return AppKit.Evm.ReadContractAsync<BigInteger>(
                contractAddress,
                TcoinAbi,
                function,
                arguments
            );
        return AppKit.Evm.ReadContractAsync<BigInteger>(
            contractAddress,
            TankGameAbi,
            function,
            arguments
        );
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private async Task<BigInteger> ReadBrowserUInt256(
        string contractAddress,
        string data)
    {
        string result = await SendBrowserWalletRequest(
            requestId => AbramsWalletReadContract(
                gameObject.name,
                requestId,
                contractAddress,
                data
            )
        );
        if (string.IsNullOrEmpty(result) ||
            !result.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The browser wallet returned invalid contract data."
            );
        }

        string value = result.Substring(2);
        return value.Length == 0
            ? BigInteger.Zero
            : BigInteger.Parse(
                "0" + value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture
            );
    }

    private static string EncodeContractCall(
        string functionSelector,
        object[] arguments)
    {
        if (functionSelector == null ||
            !Regex.IsMatch(functionSelector, "^0x[0-9a-fA-F]{8}$"))
        {
            throw new ArgumentException(
                "A four-byte EVM function selector is required.",
                nameof(functionSelector)
            );
        }

        System.Text.StringBuilder data =
            new System.Text.StringBuilder(functionSelector);
        foreach (object argument in arguments)
        {
            if (argument is string address && IsWalletAddress(address))
            {
                data.Append(
                    address.Substring(2).ToLowerInvariant().PadLeft(64, '0')
                );
                continue;
            }

            if (!BigInteger.TryParse(
                    Convert.ToString(
                        argument,
                        CultureInfo.InvariantCulture
                    ),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out BigInteger integer
                ) ||
                integer < BigInteger.Zero)
            {
                throw new ArgumentException(
                    "Contract arguments must be unsigned integers or addresses."
                );
            }

            string word = integer.ToString("x", CultureInfo.InvariantCulture);
            if (word.Length > 64)
                throw new ArgumentOutOfRangeException(nameof(arguments));
            data.Append(word.PadLeft(64, '0'));
        }
        return data.ToString();
    }
#endif

    private IEnumerator RefreshWalletEconomy()
    {
        if (!CanUseWalletEconomy())
            yield break;

        Task<BigInteger> balanceTask = null;
        try
        {
            balanceTask = ReadWalletUInt256(
                tankCoinContractAddress,
                "balanceOf",
                connectedWalletAddress
            );
        }
        catch (Exception exception)
        {
            walletEconomyStatus =
                "Could not read TCOIN balance: " + exception.Message;
            Debug.LogException(exception, this);
            yield break;
        }

        while (!balanceTask.IsCompleted)
            yield return null;
        if (balanceTask.IsFaulted || balanceTask.IsCanceled)
        {
            Exception exception = balanceTask.Exception;
            walletEconomyStatus =
                "Could not read TCOIN balance: " +
                (exception == null ? "request cancelled" : exception.GetBaseException().Message);
            if (exception != null)
                Debug.LogException(exception, this);
            yield break;
        }

        tcoinBalanceLabel = FormatTokenAmount(balanceTask.Result) + " TCOIN";
        for (uint equipmentId = 1; equipmentId <= equipmentInventory.Length;
             equipmentId++)
        {
            Task<BigInteger> inventoryTask = null;
            Task<BigInteger> levelTask = null;
            try
            {
                inventoryTask = ReadWalletUInt256(
                    tankGameContractAddress,
                    "getPlayerEquipmentBalance",
                    connectedWalletAddress,
                    equipmentId
                );
                levelTask = ReadWalletUInt256(
                    tankGameContractAddress,
                    "playerEquipmentLevel",
                    connectedWalletAddress,
                    equipmentId
                );
            }
            catch (Exception exception)
            {
                walletEconomyStatus =
                    "Could not load equipment inventory: " + exception.Message;
                Debug.LogException(exception, this);
                yield break;
            }

            while (!inventoryTask.IsCompleted || !levelTask.IsCompleted)
                yield return null;
            if (inventoryTask.IsFaulted || inventoryTask.IsCanceled ||
                levelTask.IsFaulted || levelTask.IsCanceled)
            {
                Exception exception = inventoryTask.Exception ??
                    levelTask.Exception;
                walletEconomyStatus =
                    "Could not load equipment inventory: " +
                    (exception == null ? "request cancelled" : exception.GetBaseException().Message);
                if (exception != null)
                    Debug.LogException(exception, this);
                yield break;
            }

            equipmentInventory[equipmentId - 1] = inventoryTask.Result;
            equipmentLevels[equipmentId - 1] =
                (byte)levelTask.Result;
        }

        walletEconomyStatus = "Wallet and equipment inventory refreshed.";
    }

    private bool CanUseWalletEconomy()
    {
        if (!walletKitReady || !IsWalletConnected() ||
            string.IsNullOrEmpty(connectedWalletAddress))
        {
            walletEconomyStatus = "Connect a wallet to continue.";
            return false;
        }
        if (!IsWalletOnEthereumSepolia())
        {
            walletEconomyStatus =
                "Switch your wallet to Ethereum Sepolia before transacting.";
            return false;
        }
        if (string.IsNullOrEmpty(onlineAuthToken))
        {
            walletEconomyStatus = "Sign in before using the TCOIN economy.";
            return false;
        }
        if (!string.Equals(
                onlineWalletAddress,
                connectedWalletAddress,
                StringComparison.OrdinalIgnoreCase))
        {
            walletEconomyStatus =
                "Link this wallet to your signed-in account first.";
            return false;
        }
        if (!walletContractsReady)
        {
            walletEconomyStatus =
                "TCOIN actions are disabled because the configured contracts " +
                "are not deployed on Ethereum Sepolia.";
            return false;
        }
        return true;
    }

    private bool IsWalletOnEthereumSepolia()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return browserWalletConnected &&
            string.Equals(
                browserWalletChainId,
                "0xaa36a7",
                StringComparison.OrdinalIgnoreCase
            );
#else
        return AppKit.IsAccountConnected &&
            string.Equals(
                AppKit.AccountController.ChainId,
                "eip155:11155111",
                StringComparison.Ordinal
            );
#endif
    }

    private bool IsWalletConnected()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return walletKitReady && browserWalletConnected;
#else
        return walletKitReady && AppKit.IsAccountConnected;
#endif
    }

    private string WalletConnectButtonLabel()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return IsWalletConnected()
            ? "METAMASK CONNECTED"
            : "CONNECT METAMASK";
#else
        return IsWalletConnected()
            ? "WALLET CONNECTED"
            : "CONNECT WALLET";
#endif
    }

    private string WalletSearchButtonLabel()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "USE METAMASK EXTENSION";
#else
        return "FIND METAMASK";
#endif
    }

    private void RunWalletAction(
        string description,
        Func<Task<string>> action)
    {
        if (onlineWalletActionInProgress || !CanUseWalletEconomy())
            return;
        StartCoroutine(ExecuteWalletAction(description, action));
    }

    private IEnumerator ExecuteWalletAction(
        string description,
        Func<Task<string>> action)
    {
        onlineWalletActionInProgress = true;
        walletEconomyStatus = description + " — confirm in your wallet.";
        Task<string> transactionTask = null;
        try
        {
            transactionTask = action();
        }
        catch (Exception exception)
        {
            onlineWalletActionInProgress = false;
            walletEconomyStatus = description + " failed: " + exception.Message;
            Debug.LogException(exception, this);
            yield break;
        }

        while (!transactionTask.IsCompleted)
            yield return null;

        if (transactionTask.IsFaulted || transactionTask.IsCanceled)
        {
            Exception exception = transactionTask.Exception;
            onlineWalletActionInProgress = false;
            walletEconomyStatus = transactionTask.IsCanceled
                ? description + " was cancelled."
                : description + " failed: " +
                    (exception == null ? "unknown error" : exception.GetBaseException().Message);
            if (exception != null)
                Debug.LogException(exception, this);
            yield break;
        }

        walletEconomyStatus =
            description + " submitted: " + transactionTask.Result;
        onlineWalletActionInProgress = false;
        yield return new WaitForSecondsRealtime(3f);
        yield return RefreshWalletEconomy();
    }

    private Task<string> WriteWalletTransaction(
        string contractAddress,
        string abi,
        string function,
        BigInteger gas,
        object[] arguments)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return SendBrowserTransaction(
            contractAddress,
            GetWalletFunctionSelector(function),
            arguments,
            BigInteger.Zero,
            gas
        );
#else
        return AppKit.Evm.WriteContractAsync(
            contractAddress,
            abi,
            function,
            gas,
            arguments
        );
#endif
    }

    private Task<string> WriteWalletTransaction(
        string contractAddress,
        string abi,
        string function,
        BigInteger value,
        BigInteger gas)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return SendBrowserTransaction(
            contractAddress,
            GetWalletFunctionSelector(function),
            new object[0],
            value,
            gas
        );
#else
        return AppKit.Evm.WriteContractAsync(
            contractAddress,
            abi,
            function,
            value: value,
            gas: gas
        );
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    private Task<string> SendBrowserTransaction(
        string contractAddress,
        string selector,
        object[] arguments,
        BigInteger value,
        BigInteger gas)
    {
        string data = EncodeContractCall(selector, arguments);
        return SendBrowserWalletRequest(
            requestId => AbramsWalletSendTransaction(
                gameObject.name,
                requestId,
                contractAddress,
                data,
                ToHexQuantity(value),
                ToHexQuantity(gas)
            )
        );
    }

    private static string GetWalletFunctionSelector(string function)
    {
        switch (function)
        {
            case "purchaseCoin": return "0x6426a642";
            case "transfer": return "0xa9059cbb";
            case "approve": return "0x095ea7b3";
            case "buyEquipment": return "0xdaeee049";
            case "upgradeEquipment": return "0x562b3800";
            case "equipItem": return "0x7e14a4d5";
            case "unequipItem": return "0x8266741c";
            case "listEquipment": return "0xf74889c7";
            case "buyListedEquipment": return "0x033c701a";
            case "cancelListing": return "0x305a67a8";
            default:
                throw new ArgumentException(
                    "Unsupported wallet transaction: " + function,
                    nameof(function)
                );
        }
    }

    private static string ToHexQuantity(BigInteger value)
    {
        if (value < BigInteger.Zero)
            throw new ArgumentOutOfRangeException(nameof(value));
        return "0x" + value.ToString("x", CultureInfo.InvariantCulture);
    }
#endif

    private void PurchaseTcoin()
    {
        if (!TryParse18DigitAmount(ethPurchaseAmountInput, out BigInteger amount) ||
            amount <= BigInteger.Zero)
        {
            walletEconomyStatus = "Enter a positive ETH purchase amount.";
            return;
        }

        RunWalletAction(
            "TCOIN purchase",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "purchaseCoin",
                value: amount,
                gas: new BigInteger(200000)
            )
        );
    }

    private void TransferTcoin()
    {
        if (!IsWalletAddress(tcoinTransferAddressInput) ||
            !TryParse18DigitAmount(
                tcoinTransferAmountInput,
                out BigInteger amount
            ) ||
            amount <= BigInteger.Zero)
        {
            walletEconomyStatus =
                "Enter a valid recipient address and positive TCOIN amount.";
            return;
        }

        string recipient = tcoinTransferAddressInput.Trim();
        RunWalletAction(
            "TCOIN transfer",
            () => WriteWalletTransaction(
                tankCoinContractAddress,
                TcoinAbi,
                "transfer",
                new BigInteger(90000),
                new object[] { recipient, amount }
            )
        );
    }

    private void ApproveEquipmentSpend()
    {
        if (!TryParse18DigitAmount(
                equipmentApprovalAmountInput,
                out BigInteger amount
            ) ||
            amount <= BigInteger.Zero)
        {
            walletEconomyStatus =
                "Enter the exact TCOIN amount you intend to approve.";
            return;
        }

        RunWalletAction(
            "TCOIN allowance approval",
            () => WriteWalletTransaction(
                tankCoinContractAddress,
                TcoinAbi,
                "approve",
                new BigInteger(90000),
                new object[] { tankGameContractAddress, amount }
            )
        );
    }

    private void BuyEquipment()
    {
        if (!TryParseEquipmentInputs(
                out uint equipmentId,
                out uint amount
            ))
            return;

        RunWalletAction(
            "Equipment purchase",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "buyEquipment",
                new BigInteger(160000),
                new object[] { equipmentId, amount }
            )
        );
    }

    private void UpgradeEquipment()
    {
        if (!uint.TryParse(
                equipmentIdInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out uint equipmentId
            ) ||
            equipmentId == 0)
        {
            walletEconomyStatus = "Enter an equipment ID from 1 to 5.";
            return;
        }

        RunWalletAction(
            "Equipment upgrade",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "upgradeEquipment",
                new BigInteger(160000),
                new object[] { equipmentId }
            )
        );
    }

    private void EquipEquipment()
    {
        if (!TryParseEquipmentInputs(out uint equipmentId, out _) ||
            !byte.TryParse(
                equipmentSlotInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out byte slot
            ) ||
            slot < 1 ||
            slot > 3)
        {
            walletEconomyStatus =
                "Enter an owned equipment ID and slot (1 weapon, 2 armor, 3 engine).";
            return;
        }

        RunWalletAction(
            "Equip equipment",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "equipItem",
                new BigInteger(120000),
                new object[] { equipmentId, slot }
            )
        );
    }

    private void UnequipEquipment()
    {
        if (!byte.TryParse(
                equipmentSlotInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out byte slot
            ) ||
            slot < 1 ||
            slot > 3)
        {
            walletEconomyStatus = "Enter a slot from 1 to 3.";
            return;
        }

        RunWalletAction(
            "Unequip equipment",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "unequipItem",
                new BigInteger(100000),
                new object[] { slot }
            )
        );
    }

    private void ListEquipment()
    {
        if (!TryParseEquipmentInputs(
                out uint equipmentId,
                out uint amount
            ) ||
            !TryParse18DigitAmount(
                marketplacePriceInput,
                out BigInteger price
            ) ||
            price <= BigInteger.Zero)
        {
            walletEconomyStatus =
                "Enter a valid equipment ID, amount, and positive TCOIN price.";
            return;
        }

        RunWalletAction(
            "Equipment listing",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "listEquipment",
                new BigInteger(180000),
                new object[] { equipmentId, amount, price }
            )
        );
    }

    private void BuyListedEquipment()
    {
        if (!uint.TryParse(
                marketplaceListingIdInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out uint listingId
            ) ||
            listingId == 0 ||
            !uint.TryParse(
                marketplaceBuyAmountInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out uint amount
            ) ||
            amount == 0)
        {
            walletEconomyStatus =
                "Enter a listing ID and a positive purchase amount.";
            return;
        }

        RunWalletAction(
            "Marketplace purchase",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "buyListedEquipment",
                new BigInteger(200000),
                new object[] { listingId, amount }
            )
        );
    }

    private void CancelEquipmentListing()
    {
        if (!uint.TryParse(
                marketplaceListingIdInput,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out uint listingId
            ) ||
            listingId == 0)
        {
            walletEconomyStatus = "Enter a valid listing ID.";
            return;
        }

        RunWalletAction(
            "Cancel marketplace listing",
            () => WriteWalletTransaction(
                tankGameContractAddress,
                TankGameAbi,
                "cancelListing",
                new BigInteger(120000),
                new object[] { listingId }
            )
        );
    }

    private bool TryParseEquipmentInputs(
        out uint equipmentId,
        out uint amount)
    {
        bool validId = uint.TryParse(
            equipmentIdInput,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out equipmentId
        ) && equipmentId > 0 && equipmentId <= 5;
        bool validAmount = uint.TryParse(
            equipmentAmountInput,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out amount
        ) && amount > 0;
        if (!validId || !validAmount)
        {
            walletEconomyStatus =
                "Enter equipment ID 1-5 and a positive item amount.";
            return false;
        }
        return true;
    }

    private static bool TryParse18DigitAmount(
        string text,
        out BigInteger amount)
    {
        amount = BigInteger.Zero;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string trimmed = text.Trim();
        string[] parts = trimmed.Split('.');
        if (parts.Length > 2 || parts[0].Length == 0)
            return false;
        if (!Regex.IsMatch(parts[0], @"^\d+$"))
            return false;
        string fractional = parts.Length == 2 ? parts[1] : "";
        if (fractional.Length > 18 ||
            (fractional.Length > 0 && !Regex.IsMatch(fractional, @"^\d+$")))
            return false;

        if (!BigInteger.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out BigInteger whole))
            return false;
        string paddedFraction = fractional.PadRight(18, '0');
        BigInteger fraction = paddedFraction.Length == 0
            ? BigInteger.Zero
            : BigInteger.Parse(
                paddedFraction,
                CultureInfo.InvariantCulture
            );
        amount = whole * BigInteger.Pow(10, 18) + fraction;
        return true;
    }

    private static bool IsWalletAddress(string address)
    {
        return !string.IsNullOrWhiteSpace(address) &&
            Regex.IsMatch(address.Trim(), "^0x[0-9a-fA-F]{40}$");
    }

    private static string FormatTokenAmount(BigInteger amount)
    {
        BigInteger scale = BigInteger.Pow(10, 18);
        BigInteger whole = BigInteger.Divide(amount, scale);
        string fraction = BigInteger.Remainder(amount, scale)
            .ToString("D18", CultureInfo.InvariantCulture)
            .TrimEnd('0');
        return fraction.Length == 0
            ? whole.ToString(CultureInfo.InvariantCulture)
            : whole.ToString(CultureInfo.InvariantCulture) + "." + fraction;
    }
}
