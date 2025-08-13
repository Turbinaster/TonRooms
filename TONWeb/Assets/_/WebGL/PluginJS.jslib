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
	}
});