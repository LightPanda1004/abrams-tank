mergeInto(LibraryManager.library, {
  $AbramsGameSockets: {},

  AbramsGameSocketConnect__deps: ["$AbramsGameSockets"],
  AbramsGameSocketConnect: function (targetPtr, urlPtr) {
    var target = UTF8ToString(targetPtr);
    var url = UTF8ToString(urlPtr);
    var previous = AbramsGameSockets[target];
    if (previous) {
      previous.close();
    }

    var socket;
    try {
      socket = new WebSocket(url);
    } catch (error) {
      SendMessage(
        target,
        "OnGameSocketMessage",
        JSON.stringify({
          type: "error",
          message: String(error && error.message ? error.message : error)
        })
      );
      return;
    }

    AbramsGameSockets[target] = socket;
    socket.onopen = function () {
      if (AbramsGameSockets[target] !== socket) {
        return;
      }
      SendMessage(target, "OnGameSocketMessage", "{\"type\":\"open\"}");
    };
    socket.onmessage = function (event) {
      if (AbramsGameSockets[target] === socket) {
        SendMessage(target, "OnGameSocketMessage", String(event.data));
      }
    };
    socket.onerror = function () {
      if (AbramsGameSockets[target] === socket) {
        SendMessage(
          target,
          "OnGameSocketMessage",
          "{\"type\":\"error\",\"message\":\"WebSocket connection failed.\"}"
        );
      }
    };
    socket.onclose = function (event) {
      if (AbramsGameSockets[target] !== socket) {
        return;
      }
      delete AbramsGameSockets[target];
      SendMessage(
        target,
        "OnGameSocketMessage",
        JSON.stringify({
          type: "close",
          code: event.code,
          reason: event.reason || ""
        })
      );
    };
  },

  AbramsGameSocketSend__deps: ["$AbramsGameSockets"],
  AbramsGameSocketSend: function (targetPtr, messagePtr) {
    var target = UTF8ToString(targetPtr);
    var message = UTF8ToString(messagePtr);
    var socket = AbramsGameSockets[target];
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(message);
    }
  },

  AbramsGameSocketClose__deps: ["$AbramsGameSockets"],
  AbramsGameSocketClose: function (targetPtr) {
    var target = UTF8ToString(targetPtr);
    var socket = AbramsGameSockets[target];
    if (socket) {
      delete AbramsGameSockets[target];
      socket.close(1000, "Client closed the room.");
    }
  }
});
