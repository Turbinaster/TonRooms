mergeInto(LibraryManager.library, {
        // Method used to send a message to the page
        SendMessageToPage: function (text) {
                // Convert bytes to the text
                var convertedText = Pointer_stringify(text);
                // Pass message to the page
                receiveMessageFromUnity(convertedText); // This function is embeded into the page
        },
        ShowFriendsTeleport: function () {
                showFriendsTeleport();
        },
        HideFriendsTeleport: function () {
                hideFriendsTeleport();
        },
        SendMessageToPage1: function (text) {
                // Convert bytes to the text
                var convertedText = Pointer_stringify(text);
                // Pass message to the page
                receiveMessageFromUnity1(convertedText); // This function is embeded into the page
        },
        LoadImageToPng: function (urlPtr, receiverPtr, successPtr, errorPtr, requestIdPtr) {
                var toJsString = typeof UTF8ToString === 'function' ? UTF8ToString : Pointer_stringify;
                var url = toJsString(urlPtr);
                var receiver = toJsString(receiverPtr);
                var successCallback = toJsString(successPtr);
                var errorCallback = toJsString(errorPtr);
                var requestId = toJsString(requestIdPtr);
                var image = new Image();
                image.crossOrigin = 'anonymous';
                image.onload = function () {
                        try {
                                var canvas = document.createElement('canvas');
                                canvas.width = image.width;
                                canvas.height = image.height;
                                var context = canvas.getContext('2d');
                                context.drawImage(image, 0, 0);
                                var dataUrl = canvas.toDataURL('image/png');
                                var base64Data = dataUrl.split(',')[1];
                                Module.SendMessage(receiver, successCallback, requestId + '|' + base64Data);
                        } catch (error) {
                                var errorMessage = (error && error.message) ? error.message : 'Unknown image conversion error';
                                Module.SendMessage(receiver, errorCallback, requestId + '|' + errorMessage);
                        }
                };
                image.onerror = function () {
                        Module.SendMessage(receiver, errorCallback, requestId + '|Failed to load ' + url);
                };
                image.src = url;
        }
});
