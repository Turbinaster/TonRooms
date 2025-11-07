(function (window, $) {
    if (!window.TonConnectSDK) {
        console.error('TonConnect SDK is not loaded');
        return;
    }

    var manifestUrl = window.tonConnectManifestUrl || (window.location.origin + '/tonconnect-manifest.json');
    var connector = new window.TonConnectSDK.TonConnect({ manifestUrl: manifestUrl });
    var connectPromise = null;
    var walletsCache = null;
    var modalElement = null;
    var qrInstance = null;
    var initialized = false;

    var manager = {
        init: init,
        ensureConnected: ensureConnected,
        disconnect: disconnect,
        getWallet: function () { return connector.wallet; },
        registerSession: registerSession,
        formatAddress: formatAddress,
        connector: connector
    };

    function init() {
        if (initialized) {
            return manager;
        }
        initialized = true;

        connector.restoreConnection().catch(function (error) {
            console.error('TonConnect restore error', error);
        });

        connector.onStatusChange(function () {
            updateUi();
            if (connector.wallet) {
                hideModal();
            }
        }, function (error) {
            hideModal();
            if (error instanceof window.TonConnectSDK.UserRejectsError) {
                if (typeof window.er === 'function') {
                    window.er('Подключение отменено в кошельке');
                }
            } else {
                console.error('TonConnect status error', error);
            }
        });

        $(document).on('click', '[data-tonconnect-connect]', function (event) {
            event.preventDefault();
            ensureConnected().catch(function (error) {
                if (error instanceof window.TonConnectSDK.UserRejectsError) {
                    if (typeof window.er === 'function') {
                        window.er('Подключение отклонено пользователем');
                    }
                } else if (error) {
                    console.error('TonConnect connect error', error);
                    if (typeof window.er === 'function') {
                        window.er('Не удалось подключить TON-кошелек');
                    }
                }
            });
        });

        $(document).on('click', '[data-tonconnect-disconnect]', function (event) {
            event.preventDefault();
            disconnect().catch(function (error) {
                console.error('TonConnect disconnect error', error);
            });
        });

        updateUi();
        return manager;
    }

    function ensureConnected() {
        if (connector.wallet) {
            return Promise.resolve(connector.wallet);
        }
        if (connectPromise) {
            return connectPromise;
        }

        connectPromise = new Promise(function (resolve, reject) {
            var unsubscribe = connector.onStatusChange(function (wallet) {
                if (wallet) {
                    cleanup();
                    resolve(wallet);
                }
            }, function (error) {
                cleanup();
                reject(error);
            });

            startConnection().catch(function (error) {
                cleanup();
                reject(error);
            });

            function cleanup() {
                connectPromise = null;
                if (typeof unsubscribe === 'function') {
                    unsubscribe();
                }
                hideModal();
            }
        });

        return connectPromise;
    }

    function disconnect() {
        if (!connector.connected) {
            updateUi();
            return Promise.resolve();
        }
        return connector.disconnect().then(function () {
            updateUi();
        });
    }

    function startConnection() {
        return loadWallets().then(function (walletData) {
            if (!walletData || !walletData.wallets.length) {
                throw new Error('Нет доступных кошельков для подключения');
            }

            if (!isDesktop() && walletData.embedded) {
                connector.connect({ jsBridgeKey: walletData.embedded.jsBridgeKey });
                return;
            }

            var remoteWallet = walletData.wallets.find(function (wallet) {
                var appName = (wallet.appName || '').toLowerCase();
                var name = (wallet.name || '').toLowerCase();
                return (appName === 'tonkeeper' || name.indexOf('tonkeeper') !== -1) && wallet.universalLink && wallet.bridgeUrl;
            });

            if (!remoteWallet) {
                remoteWallet = walletData.wallets.find(function (wallet) {
                    return wallet.universalLink && wallet.bridgeUrl;
                });
            }

            if (!remoteWallet) {
                throw new Error('Не найден удаленный кошелек TonConnect');
            }

            var universalLink = connector.connect({
                universalLink: remoteWallet.universalLink,
                bridgeUrl: remoteWallet.bridgeUrl
            });

            if (isMobile()) {
                openLink(addReturnStrategy(universalLink, 'none'), '_blank');
            } else {
                showModal(universalLink);
            }
        });
    }

    function loadWallets() {
        if (walletsCache) {
            return Promise.resolve(walletsCache);
        }
        return connector.getWallets().then(function (wallets) {
            var embedded = wallets.filter(function (wallet) { return wallet.embedded; })[0] || null;
            walletsCache = {
                wallets: wallets,
                embedded: embedded
            };
            return walletsCache;
        });
    }

    function registerSession(options) {
        if (!options || !options.session || !options.address || !options.url) {
            return Promise.reject('registerSession: invalid parameters');
        }
        return new Promise(function (resolve, reject) {
            if (typeof window.post === 'function') {
                window.post(options.url, {
                    session: options.session,
                    address: options.address
                }, function (data) {
                    if (data && data.r === 'ok') {
                        resolve(data);
                    } else if (data && data.m) {
                        reject(new Error(data.m));
                    } else {
                        reject(new Error('Неизвестная ошибка регистрации сессии'));
                    }
                });
            } else {
                $.ajax({
                    url: options.url,
                    type: 'POST',
                    data: {
                        session: options.session,
                        address: options.address
                    }
                }).done(function (data) {
                    if (data && data.r === 'ok') {
                        resolve(data);
                    } else if (data && data.m) {
                        reject(new Error(data.m));
                    } else {
                        reject(new Error('Неизвестный ответ сервера'));
                    }
                }).fail(function (xhr) {
                    reject(new Error(xhr.responseText || 'Не удалось зарегистрировать сессию'));
                });
            }
        });
    }

    function showModal(universalLink) {
        var modal = getModal();
        var $modal = $(modal);
        var qrContainer = $modal.find('.tonconnect-modal__qr')[0];
        if (!window.QRCode) {
            console.error('QRCode library is not loaded');
            return;
        }
        if (!qrInstance) {
            qrInstance = new window.QRCode(qrContainer, {
                text: universalLink,
                width: 256,
                height: 256,
                colorDark: '#000000',
                colorLight: '#ffffff'
            });
        } else {
            qrInstance.clear();
            qrInstance.makeCode(universalLink);
        }
        $modal.addClass('visible');
    }

    function hideModal() {
        if (modalElement) {
            $(modalElement).removeClass('visible');
        }
    }

    function getModal() {
        if (modalElement) {
            return modalElement;
        }
        var modal = document.createElement('div');
        modal.className = 'tonconnect-modal';
        modal.innerHTML = '' +
            '<div class="tonconnect-modal__content">' +
            '  <button type="button" class="tonconnect-modal__close" aria-label="Close">×</button>' +
            '  <div class="tonconnect-modal__title">Сканируйте QR-код в Tonkeeper</div>' +
            '  <div class="tonconnect-modal__qr"></div>' +
            '  <div class="tonconnect-modal__footer">Отсканируйте код в приложении Tonkeeper, чтобы подключить кошелек.</div>' +
            '</div>';
        document.body.appendChild(modal);
        modalElement = modal;

        $(modal).on('click', '.tonconnect-modal__close', function () {
            hideModal();
        });
        $(modal).on('click', function (event) {
            if (event.target === modal) {
                hideModal();
            }
        });
        return modal;
    }

    function updateUi() {
        var wallet = connector.wallet;
        var addressElement = $('[data-tonconnect-address]');
        var badgeElement = $('[data-tonconnect-network]');
        var connectButtons = $('[data-tonconnect-connect]');
        var connectedBlocks = $('[data-tonconnect-connected]');
        var disconnectButtons = $('[data-tonconnect-disconnect]');

        if (wallet) {
            var friendly = formatAddress(wallet.account.address, wallet.account.chain);
            addressElement.text(friendly).attr('title', friendly ? wallet.account.address : '');
            var chainLabel = wallet.account.chain === window.TonConnectSDK.CHAIN.TESTNET ? 'testnet' : 'mainnet';
            badgeElement.text(chainLabel);
            badgeElement.addClass('visible');
            connectButtons.hide();
            connectedBlocks.show();
            disconnectButtons.prop('disabled', false);
        } else {
            addressElement.text('');
            badgeElement.removeClass('visible').text('');
            connectedBlocks.hide();
            connectButtons.show();
        }
    }

    function formatAddress(address, chain) {
        if (!address) {
            return '';
        }
        try {
            var friendly = window.TonConnectSDK.toUserFriendlyAddress(address, chain === window.TonConnectSDK.CHAIN.TESTNET);
            return friendly.slice(0, 4) + '...' + friendly.slice(-4);
        } catch (error) {
            console.warn('Failed to format address', error);
            return address;
        }
    }

    function isMobile() {
        return window.innerWidth <= 500 || /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
    }

    function isDesktop() {
        return window.innerWidth >= 1050;
    }

    function openLink(href, target) {
        if (target === void 0) {
            target = '_self';
        }
        window.open(href, target, 'noreferrer noopener');
    }

    function addReturnStrategy(link, strategy) {
        try {
            var url = new URL(link);
            url.searchParams.set('ret', strategy);
            return url.toString();
        } catch (error) {
            return link + (link.indexOf('?') === -1 ? '?' : '&') + 'ret=' + encodeURIComponent(strategy);
        }
    }

    window.TonConnectManager = manager;
    init();
})(window, jQuery);
