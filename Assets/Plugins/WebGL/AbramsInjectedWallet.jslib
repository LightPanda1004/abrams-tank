mergeInto(LibraryManager.library, {
  $AbramsInjectedWallet: {
    provider: null,
    target: "",

    getProvider: function () {
      if (!window.ethereum) {
        return null;
      }

      var providers = window.ethereum.providers;
      if (providers && providers.length) {
        for (var i = 0; i < providers.length; i++) {
          if (providers[i].isMetaMask) {
            return providers[i];
          }
        }
      }
      return window.ethereum.isMetaMask ? window.ethereum : null;
    },

    publishState: function (accounts, chainId, error) {
      var address = accounts && accounts.length ? accounts[0] : "";
      SendMessage(
        AbramsInjectedWallet.target,
        "OnBrowserWalletStateChanged",
        JSON.stringify({
          address: address,
          chainId: chainId || "",
          error: error || ""
        })
      );
    },

    ensureSepolia: async function (provider) {
      var chainId = await provider.request({ method: "eth_chainId" });
      if (chainId.toLowerCase() !== "0xaa36a7") {
        try {
          await provider.request({
            method: "wallet_switchEthereumChain",
            params: [{ chainId: "0xaa36a7" }]
          });
        } catch (error) {
          if (error.code !== 4902) {
            throw error;
          }
          await provider.request({
            method: "wallet_addEthereumChain",
            params: [{
              chainId: "0xaa36a7",
              chainName: "Sepolia",
              nativeCurrency: {
                name: "Sepolia Ether",
                symbol: "ETH",
                decimals: 18
              },
              rpcUrls: ["https://ethereum-sepolia-rpc.publicnode.com"],
              blockExplorerUrls: ["https://sepolia.etherscan.io"]
            }]
          });
          await provider.request({
            method: "wallet_switchEthereumChain",
            params: [{ chainId: "0xaa36a7" }]
          });
        }
      }
      return await provider.request({ method: "eth_chainId" });
    },

    reportRequest: function (target, requestId, result, error) {
      var message = error && error.code === 4001
        ? "MetaMask request was rejected."
        : String(error && error.message ? error.message : error || "");
      SendMessage(
        target,
        "OnBrowserWalletRequestCompleted",
        JSON.stringify({
          requestId: requestId,
          result: result || "",
          error: message
        })
      );
    }
  },

  AbramsWalletConnect__deps: ["$AbramsInjectedWallet"],
  AbramsWalletConnect: function (targetPtr) {
    var target = UTF8ToString(targetPtr);
    AbramsInjectedWallet.target = target;
    var provider = AbramsInjectedWallet.getProvider();
    if (!provider) {
      AbramsInjectedWallet.publishState(
        [],
        "",
        "MetaMask was not detected. Open this game in Chrome with the MetaMask extension enabled."
      );
      return;
    }
    AbramsInjectedWallet.provider = provider;

    if (!provider.__abramsWalletListenerInstalled) {
      provider.__abramsWalletListenerInstalled = true;
      provider.on("accountsChanged", function (accounts) {
        provider.request({ method: "eth_chainId" })
          .then(function (chainId) {
            AbramsInjectedWallet.publishState(accounts, chainId, "");
          })
          .catch(function (error) {
            AbramsInjectedWallet.publishState([], "", String(error));
          });
      });
      provider.on("chainChanged", function (chainId) {
        provider.request({ method: "eth_accounts" })
          .then(function (accounts) {
            AbramsInjectedWallet.publishState(accounts, chainId, "");
          })
          .catch(function (error) {
            AbramsInjectedWallet.publishState([], chainId, String(error));
          });
      });
    }

    provider.request({ method: "eth_requestAccounts" })
      .then(async function (accounts) {
        try {
          var chainId =
            await AbramsInjectedWallet.ensureSepolia(provider);
          AbramsInjectedWallet.publishState(accounts, chainId, "");
        } catch (error) {
          var currentChain = "";
          try {
            currentChain = await provider.request({
              method: "eth_chainId"
            });
          } catch (chainError) {
            currentChain = "";
          }
          AbramsInjectedWallet.publishState(
            accounts,
            currentChain,
            error && error.code === 4001
              ? "Network switch was rejected. Switch MetaMask to Ethereum Sepolia."
              : String(error && error.message ? error.message : error)
          );
        }
      })
      .catch(function (error) {
        AbramsInjectedWallet.publishState(
          [],
          "",
          error && error.code === 4001
            ? "MetaMask connection was rejected."
            : String(error && error.message ? error.message : error)
        );
      });
  },

  AbramsWalletSignMessage__deps: ["$AbramsInjectedWallet"],
  AbramsWalletSignMessage: function (
    targetPtr,
    requestId,
    addressPtr,
    messagePtr
  ) {
    var target = UTF8ToString(targetPtr);
    var address = UTF8ToString(addressPtr);
    var message = UTF8ToString(messagePtr);
    var provider = AbramsInjectedWallet.provider;
    if (!provider) {
      AbramsInjectedWallet.reportRequest(
        target,
        requestId,
        "",
        new Error("Connect MetaMask first.")
      );
      return;
    }

    provider.request({
      method: "personal_sign",
      params: [message, address]
    }).then(function (signature) {
      AbramsInjectedWallet.reportRequest(
        target,
        requestId,
        signature,
        ""
      );
    }).catch(function (error) {
      AbramsInjectedWallet.reportRequest(target, requestId, "", error);
    });
  },

  AbramsWalletReadContract__deps: ["$AbramsInjectedWallet"],
  AbramsWalletReadContract: function (
    targetPtr,
    requestId,
    addressPtr,
    dataPtr
  ) {
    var target = UTF8ToString(targetPtr);
    var address = UTF8ToString(addressPtr);
    var data = UTF8ToString(dataPtr);
    var provider = AbramsInjectedWallet.provider;
    if (!provider) {
      AbramsInjectedWallet.reportRequest(
        target,
        requestId,
        "",
        new Error("Connect MetaMask first.")
      );
      return;
    }

    provider.request({
      method: "eth_call",
      params: [{ to: address, data: data }, "latest"]
    }).then(function (result) {
      AbramsInjectedWallet.reportRequest(target, requestId, result, "");
    }).catch(function (error) {
      AbramsInjectedWallet.reportRequest(target, requestId, "", error);
    });
  },

  AbramsWalletSendTransaction__deps: ["$AbramsInjectedWallet"],
  AbramsWalletSendTransaction: function (
    targetPtr,
    requestId,
    addressPtr,
    dataPtr,
    valuePtr,
    gasPtr
  ) {
    var target = UTF8ToString(targetPtr);
    var address = UTF8ToString(addressPtr);
    var data = UTF8ToString(dataPtr);
    var value = UTF8ToString(valuePtr);
    var gas = UTF8ToString(gasPtr);
    var provider = AbramsInjectedWallet.provider;
    if (!provider) {
      AbramsInjectedWallet.reportRequest(
        target,
        requestId,
        "",
        new Error("Connect MetaMask first.")
      );
      return;
    }

    provider.request({ method: "eth_chainId" })
      .then(function (chainId) {
        if (chainId.toLowerCase() !== "0xaa36a7") {
          throw new Error("Switch MetaMask to Ethereum Sepolia first.");
        }
        return provider.request({ method: "eth_accounts" });
      })
      .then(function (accounts) {
        if (!accounts || !accounts.length) {
          throw new Error("MetaMask is disconnected.");
        }
        return provider.request({
          method: "eth_sendTransaction",
          params: [{
            from: accounts[0],
            to: address,
            data: data,
            value: value,
            gas: gas
          }]
        });
      })
      .then(function (transactionHash) {
        AbramsInjectedWallet.reportRequest(
          target,
          requestId,
          transactionHash,
          ""
        );
      })
      .catch(function (error) {
        AbramsInjectedWallet.reportRequest(target, requestId, "", error);
      });
  }
});
