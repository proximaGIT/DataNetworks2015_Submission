var groupNumber = 7;
// replace by your group number
var groupPswd = "YD4G2gs7";
var defPort = "42015";

var User_Name = null;
// statuses
var DISCONNECTED = 0;
var CONNECTING = 1;
var CONNECTED = 2;
var AUTHENTICATING = 3;
var AUTHENTICATED = 4;
var status = DISCONNECTED;

var socket = null;
var userId = 0;
var msgID = 0;
var loginTryCount = 0;
var loginTryLimit = 5;

var msgTryCount = 0;
var msgTryLimit = 5;

function respondToMessage(srv_msg) {
    var lines = srv_msg.data.split("\n");
    if (status == AUTHENTICATING) {
        respondtoLoginErrors(lines);
    } else if (status == AUTHENTICATED) {
        respondtoChatErrors(lines);
    } else {
        var reply = lines[0].split(" ");
        if (reply[0] == "INVD" && reply[1] == 0) {
            addInfoMessage("Malformed Message received at the Server");
            status = DISCONNECTED;
            disconnectButtonPressed();
        }
    }
}

function respondtoLoginErrors(lines) {
    var reply = lines[0].split(" ");
    if (reply[0] == "OKAY" && reply[1] == userId) {
        status = AUTHENTICATED;
        onLoginSuccess();
        isLoggedIn = true;
    } else if (reply[0] == "FAIL" && reply[1] == userId) {
        //FAIL
        isLoggedIn = false;
        if (lines[1].trim() == "NAME") {
            addInfoMessage("The name is already in use. Please choose another name.");
            onLoginFailed();
            status = CONNECTED;
        } else if (lines[1].trim() == "PASSWORD") {
            addInfoMessage("Wrong password.");
            onLoginFailed();
            status = CONNECTED;
        } else if (lines[1].trim() == "NUMBER") {
            //NUMBER
            //generate new user id and retry until limit is reached.
            addInfoMessage("The number is Invalid. Trying Again");
            if (loginTryCount < loginTryLimit) {
                loginButtonPressed();
            } else {
                addInfoMessage("The number was invalid for " + String(loginTryLimit) + " attempts, Stop!");
                onLoginFailed();
            }
        } else {
            alert(lines[1]);
            addInfoMessage("Unknown Fail Message");
        }
    } else if (reply[0] == "INVD" && reply[1] == 0) {
        addInfoMessage("Malformed Message received at the Server");
        status = DISCONNECTED;
        disconnectButtonPressed();
    } else {
        addInfoMessage("This message should not exist");
    }

}

function respondtoChatErrors(lines) {
    var reply = lines[0].split(" ");
    if (reply[0] == "OKAY" && reply[1] == msgID) {
        markMessageConfirmed(msgID);
        setStatusBarText("Message Sent");
        msgTryCount = 0;
    } else if (reply[0] == "FAIL" && reply[1] == msgID) {
        isSent = false;
        if (lines[1] == "NUMBER") {
            addInfoMessage("The Message ID is Invalid, Trying Again");
            if (msgTryCount < msgTryLimit) {
                sendButtonPressed();
            } else {
                addInfoMessage("The Message ID was invalid for " + String(msgTryLimit) + " attempts, Stop!");
                msgTryCount = 0;
            }
        } else if (lines[1] == "LENGTH") {
            addInfoMessage("Message length too long, Consider using abbreviations");
        } else {
            addInfoMessage("Unknown Fail Message");
        }
    } else if (reply[0] == "ACKN" && reply[1] == msgID) {
        user_num1 = Number(lines[1].trim());
        user_name1 = findUserName(user_num1);
        markMessageAcknowledged(msgID, user_name1);

    } else if (reply[0] == "SEND") {
        msgID_temp = reply[1].trim();
        user_num2 = Number(lines[1].trim());
        user_name2 = findUserName(user_num2);
        msg_rcvd = lines[2].trim();
        addChatMessage(msgID_temp, user_name2, msg_rcvd, true);
        markMessageConfirmed(msgID_temp);


        ackn_data = "ACKN " + msgID_temp;
        try {
            socket.send(ackn_data);
            markMessageAcknowledged(msgID_temp,User_Name);
        } catch (exception) {
            addInfoMessage("Sending Acknowledgement to Server Failed");
        }
    } else if (reply[0] == "ARRV") {
        user_arrv_num = Number(reply[1].trim());
        addUser(user_arrv_num, lines[1], lines[2]);
        addInfoMessage(lines[1].trim() + " has joined.");

    } else if (reply[0] == "LEFT") {
        user_name3 = findUserName(Number(reply[1].trim()));
        addInfoMessage(user_name3 + " has left.");
        removeUser(Number(reply[1].trim()));
    } else if (reply[0] == "INVD" && reply[1] == 0) {
        addInfoMessage("Malformed Message received at the Server");
        status = DISCONNECTED;
        disconnectButtonPressed();
    } else {
        addInfoMessage("This message should not exist");
    }

}

function main() {
    document.getElementById("groupid").textContent = "Group " + groupNumber;

    // Insert any initialisation code here
    document.getElementById("passwordInput").value = groupPswd;
    //document.getElementById("serverInput").value = "modestchecker.net";
}

// Called when the "Connect" button is pressed
function connectButtonPressed() {
    var server = document.getElementById("serverInput").value;
    document.getElementById("connect").setAttribute("disabled", "disabled");
    setStatusBarText("Connecting to " + server + "...");

    // Connecting to server
    status = CONNECTING;
    if(server.indexOf(":") == -1){
      server = server + ":" + defPort;
    }
    
    try {
        socket = new WebSocket("ws://" + server);
        socket.onopen = function () {
            status = CONNECTED;
            onConnected(server);
        };
        socket.onmessage = function (srv_msg) {
            respondToMessage(srv_msg);
            document.getElementById("server_msgs").textContent = srv_msg.data;
        };
        socket.onclose = function () {
            status = DISCONNECTED;
            onDisconnected();
        };
    } catch (exception) {
        status = DISCONNECTED;
        onConnectionFailed();
    }
}

// Called when the "Disconnect" button is pressed
function disconnectButtonPressed() {
    document.getElementById("disconnect").setAttribute("disabled", "disabled");
    document.getElementById("login").setAttribute("disabled", "disabled");
    setStatusBarText("Disconnecting...");

    try {
        if (socket != null && socket.readyState == WebSocket.OPEN) {
            socket.close();
        }
    } catch (exception) {
        status = DISCONNECTED;
        onConnectionFailed();
    }
}

// Called when the "Log in" button is pressed
function loginButtonPressed() {
    var name = document.getElementById("nameInput").value;
    var password = document.getElementById("passwordInput").value;
    document.getElementById("login").setAttribute("disabled", "disabled");
    setStatusBarText("Authenticating...");
    User_Name = name;
    // Authenticating

    if (status == CONNECTED) {
        status = AUTHENTICATING;
        userId = getRandomInt();
        var data = "AUTH " + userId + "\r\n" + name + "\r\n" + password;
        try {
            loginTryCount++;
            socket.send(data);
        } catch (exception) {
            onLoginFailed();
        }
    } else {
        onLoginFailed();
        onConnectionFailed();
    }
}

// Called when the "Send" button is pressed
var isSent = false;
function sendButtonPressed() {
    var message = document.getElementById("messageInput").value;
    if (message == "")
        return;
    var to = "*";
    if (message.substring(0, 1) == "@") {
        var index = message.indexOf(":");
        if (index > 0) {
            var toUser = findUserNumber(message.substring(1, index));
            if (toUser !== -1)
                to = toUser;
            else {
                addInfoMessage("Unknown user: " + message.substring(1, index) + ".");
                return;
            }
            message = message.substring(index + 1);
        }
    }
    message = message.trim();
    if (message == "")
        return;
    document.getElementById("messageInput").value = "";
    setStatusBarText("Sending message...");

    // Insert your code here to send <message> to <to>
    if (status == AUTHENTICATED) {
        isSent = false;
        msgID = getRandomInt();
        var send_msg = "SEND " + msgID + "\r\n" + to + "\r\n" + message;
        try {
            msgTryCount++;
            socket.send(send_msg);
            isSent = true;
            addChatMessage(msgID, User_Name, message, isSent);
        } catch (exception) {
            isSent = false;
            addInfoMessage("Cannot Send Message, Use Pigeons");
        }

    }
}

// Use this function to get random integers for use with the Chat protocol
function getRandomInt() {
    return Math.floor(Math.random() * 9007199254740991);
}

// The remaining functions in this file are helper functions to update
// the user interface when certain actions are performed (e.g. a message
// is sent and should be displayed in the message list) or certain
// events occur (e.g. a message arrives, or a user has gone offline).
// You should not need to modify them, but you can if you want.
// You can also just delete everything (including the functions above)
// and write a new user interface on your own.

// Call this function when the connection to the server has been established
function onConnected(server) {
    if (server === undefined)
        document.getElementById("connectionStatusText").textContent = "Connected.";
    else
        document.getElementById("connectionStatusText").textContent = "Connected to " + server + ".";
    document.getElementById("connect").style.display = "none";
    document.getElementById("disconnect").style.display = "flex";
    document.getElementById("connect").removeAttribute("disabled");
    document.getElementById("login").removeAttribute("disabled");
    setStatusBarText("Connected.");
}

var isLoggedIn = false;
var suppressStatusBarUpdate = false;

// Call this function when the connection to the server has been closed
function onDisconnected() {
    document.getElementById("disconnect").style.display = "none";
    document.getElementById("connect").style.display = "flex";
    document.getElementById("connect").removeAttribute("disabled");
    document.getElementById("disconnect").removeAttribute("disabled");
    document.getElementById("login").setAttribute("disabled", "disabled");
    document.getElementById("message").setAttribute("disabled", "disabled");
    document.getElementById("userlist").setAttribute("disabled", "disabled");
    if (!suppressStatusBarUpdate)
        setStatusBarText("Disconnected.");
    suppressStatusBarUpdate = false;
    if (isLoggedIn)
        addInfoMessage("Session ended, no more messages will be received.");
    clearUsers();
    isLoggedIn = false;
}

// Call this function when the connection to the server fails (i.e. you get an error)
function onConnectionFailed() {
    setStatusBarText("Connection failed.");
    suppressStatusBarUpdate = true;
    onDisconnected();
    // onDisconnected should also get called
}

// Call this function when login was successful
function onLoginSuccess() {
    setStatusBarText("Successfully logged in.");
    document.getElementById("message").removeAttribute("disabled");
    document.getElementById("userlist").removeAttribute("disabled");
    addInfoMessage("Session started, now receiving messages.");
    isLoggedIn = true;
}

// Call this function when login failed
function onLoginFailed() {
    setStatusBarText("Login failed.");
    document.getElementById("login").removeAttribute("disabled");
    isLoggedIn = false;
}

// Call this function to add informational text to the message list
function addInfoMessage(text) {
    var msglist = document.getElementById("msglist");
    var infoDiv = document.createElement("div");
    infoDiv.className = "info";
    infoDiv.appendChild(document.createTextNode(text));
    msglist.appendChild(infoDiv);
    msglist.scrollTop = msglist.scrollHeight;
}

// Call this function to add a chat message to the message list.
// If isSent is true, then it is added as a "sent, but not confirmed"
// message; call markMessageConfirmed when the server has acknowledged
// that it received the message.
function addChatMessage(number, from, text, isSent) {
    var msglist = document.getElementById("msglist");
    var msgDiv = document.createElement("div");
    msgDiv.className = isSent ? "sent" : "received";
    msgDiv.id = "msg" + number;
    var fromDiv = document.createElement("div");
    fromDiv.className = "from";
    fromDiv.appendChild(document.createTextNode(from === null ? "Unknown user" : from));
    msgDiv.appendChild(fromDiv);
    var textDiv = document.createElement("div");
    textDiv.className = "message";
    textDiv.appendChild(document.createTextNode(text));
    msgDiv.appendChild(textDiv);
    if (isSent) {
        var readersDiv = document.createElement("div");
        readersDiv.className = "readers";
        readersDiv.id = "msg" + number + "readers";
        msgDiv.appendChild(readersDiv);
        msgDiv.style.opacity = 0.5;
    }
    msglist.appendChild(msgDiv);
    msglist.scrollTop = msglist.scrollHeight;
}

// Call this function to mark a sent message as confirmed
function markMessageConfirmed(number) {
    var msgDiv = document.getElementById("msg" + number);
    if (!msgDiv)
        return;
    msgDiv.style.opacity = 1.0;
}

// Call this function to indicate that a message has been acknowledged by a certain user
function markMessageAcknowledged(messageNumber, userName) {
    var msgReadersDiv = document.getElementById("msg" + messageNumber + "readers");
    if (!msgReadersDiv)
        return;
    markMessageConfirmed(messageNumber);
    var readerSpan = document.createElement("span");
    readerSpan.appendChild(document.createTextNode(userName));
    msgReadersDiv.appendChild(readerSpan);
}

// Call this function to change the text in the status bar
function setStatusBarText(text) {
    document.getElementById("statusbar").textContent = text;
}

var users = [];

// Call this function to show a user as online
function addUser(number, name, description) {
    users.push({
        number: number,
        name: name,
        description: description
    });
    var userlist = document.getElementById("userlist");
    var userSpan = document.createElement("span");
    userSpan.id = "user" + number;
    var userNameSpan = document.createElement("span");
    userNameSpan.className = "user-name";
    userNameSpan.appendChild(document.createTextNode(name));
    var userDescSpan = document.createElement("span");
    userDescSpan.className = "user-desc";
    userDescSpan.appendChild(document.createTextNode(description));
    userSpan.appendChild(userNameSpan);
    userSpan.appendChild(userDescSpan);
    userlist.appendChild(userSpan);
}

// Call this function when a user goes offline
function removeUser(number) {
    var userlist = document.getElementById("userlist");
    for (var i = 0; i < users.length; ++i) {
        if (users[i].number == number) {
            users.splice(i--, 1);
            var userSpan = document.getElementById("user" + number);
            if (userSpan)
                userlist.removeChild(userSpan);
        }
    }
}

// Call this function to get the number of a user with the given name.
// Returns -1 if there is no user with this name.
function findUserNumber(name) {
    for (var i = 0; i < users.length; ++i) {
        if (users[i].name == name)
            return users[i].number;
    }
    return -1;
}

// Call this function to get the name of a user with the given number.
// Returns null if there is no user with this number.
function findUserName(number) {
    for (var i = 0; i < users.length; ++i) {
        if (users[i].number == number)
            return users[i].name;
    }
    return null;
}

// Called by onDisconnected
function clearUsers() {
    var userlist = document.getElementById("userlist");
    for (var i = 0; i < users.length; ++i) {
        var userSpan = document.getElementById("user" + users[i].number);
        if (userSpan)
            userlist.removeChild(userSpan);
    }
    users = [];
}
