/* Group 7
 Muhammad Lathif Pambudi <s8mupamb@stud.uni-saarland.de> (2556858)
 Praharsha Sirsi <s8prsirs@stud.uni-saarland.de> (2557724)*/

var groupNumber = 7;
// replace by your group number
//var groupPswd = "YD4G2gs7";

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

//Function to respond to received server messages
function respondToMessage(srv_msg) {
    //Split messages w.r.t to new line
    var lines = srv_msg.data.split("\n");
    //check for connection status
    if (status == AUTHENTICATING) {
        respondtoLoginErrors(lines); //If connected and trying to login go to this function
    } else if (status == AUTHENTICATED) {
        respondtoChatErrors(lines); //If authenticated and chatting, go to this function
    } else {
        //Handling of Invalid message from Server
        var reply = lines[0].split(" ");
        if (reply[0] == "INVD" && reply[1] == 0) {
            addInfoMessage("Malformed Message received at the Server");
            status = DISCONNECTED;
            disconnectButtonPressed();
        }
    }
}
//Function to handle Login Server Messages
function respondtoLoginErrors(lines) {
    var reply = lines[0].split(" ");
    if (reply[0] == "OKAY" && reply[1] == userId) {
        status = AUTHENTICATED; //If server sends okay go to Authenticated state
        onLoginSuccess();
        isLoggedIn = true;
    } else if (reply[0] == "FAIL" && reply[1] == userId) {
        //FAIL
        isLoggedIn = false;
        if (lines[1] == "NAME") {
            addInfoMessage("The name is already in use. Please choose another name."); //Name failure
            onLoginFailed();
            status = CONNECTED;
        } else if (lines[1] == "PASSWORD") {
            addInfoMessage("Wrong password.");//Password failure
            onLoginFailed();
            status = CONNECTED;
        } else if (lines[1] == "NUMBER") {
            //NUMBER
            //generate new user id and retry until limit is reached.
            addInfoMessage("The number is Invalid. Trying Again");
            if (loginTryCount < loginTryLimit) {
                loginButtonPressed();
            } else {
                addInfoMessage("The number was invalid for " + String(loginTryLimit) + " attempts, Stop!"); //exit once limit reached
                onLoginFailed();
            }
        } else {
            addInfoMessage("Unknown Fail Message");//If an unknown fail message is received
        }
    } else if (reply[0] == "INVD" && reply[1] == 0) {
        addInfoMessage("Malformed Message received at the Server"); //If INVD message is received in Authenticating state
        status = DISCONNECTED;
        disconnectButtonPressed();
    } else {
        addInfoMessage("This message should not exist"); //Just to complete the if tree
    }

}
//Function to handle Chat and Authenticated State Server messages
function respondtoChatErrors(lines) {
    var reply = lines[0].split(" ");
    if (reply[0] == "OKAY" && reply[1] == msgID) {
        markMessageConfirmed(msgID); //Confirming that the message is received by the server
        setStatusBarText("Message Sent");
        msgTryCount = 0;
    } else if (reply[0] == "FAIL" && reply[1] == msgID) {
        isSent = false;
        if (lines[1] == "NUMBER") {
            //NUMBER
            //generate new message id and retry until limit is reached.
            addInfoMessage("The Message ID is Invalid, Trying Again");
            if (msgTryCount < msgTryLimit) {
                sendButtonPressed();
            } else {
                addInfoMessage("The Message ID was invalid for " + String(msgTryLimit) + " attempts, Stop!");//exit once limit reached
                msgTryCount = 0;
            }
        } else if (lines[1] == "LENGTH") {
            //Length Error from server
            addInfoMessage("Message length too long, Consider using abbreviations");
        } else {
            addInfoMessage("Unknown Fail Message"); //Just to complete the if tree (feels nice this way)
        }
    } else if (reply[0] == "ACKN" && reply[1] == msgID) {
        user_num1 = Number(lines[1].trim()); //Get user number after removing trailing spaces
        user_name1 = findUserName(user_num1); //Get username from user number
        markMessageAcknowledged(msgID, user_name1); //Acknowledge the message in chat box

    } else if (reply[0] == "SEND") {
        //Receiving new messages from the Server, sent by another client
        msgID_temp = reply[1].trim(); //Get Message ID
        user_num2 = Number(lines[1].trim()); //Get User Number
        user_name2 = findUserName(user_num2); //Get User Name
        msg_rcvd = lines[2].trim(); //Get message after removing trailing spaces
        addChatMessage(msgID_temp, user_name2, msg_rcvd, false); //Add the message into the chat box

        ackn_data = "ACKN " + msgID_temp; //Send back acknowledgement to server
        try {
            socket.send(ackn_data);
            markMessageAcknowledged(msgID_temp, User_Name);
        } catch (exception) {
            addInfoMessage("Sending Acknowledgement to Server Failed"); //handling the rare case if sending failed
        }
    } else if (reply[0] == "ARRV") {
        //Handling new user arrival
        user_arrv_num = Number(reply[1].trim());
        addUser(user_arrv_num, lines[1], lines[2]);
        addInfoMessage(lines[1].trim() + " has joined."); //Print in chat box if a new user has joined

    } else if (reply[0] == "LEFT") {
        //Handling of Users leaving the chat
        user_name3 = findUserName(Number(reply[1].trim()));
        addInfoMessage(user_name3 + " has left."); //Print in chat box if user has left. If it is The Terminator, he will be back
        removeUser(Number(reply[1].trim()));
    } else if (reply[0] == "INVD" && reply[1] == 0) {
        //Handling Invalid message in Authenticated State
        addInfoMessage("Malformed Message received at the Server");
        status = DISCONNECTED;
        disconnectButtonPressed();
    } else {
        addInfoMessage("This message should not exist");//Just to complete the if tree (feels nice this way)
    }

}

function main() {
    document.getElementById("groupid").textContent = "Group " + groupNumber;

    // Insert any initialisation code here
    //document.getElementById("passwordInput").value = groupPswd;
    document.getElementById("serverInput").value = "modestchecker.net:42015";
}

// Called when the "Connect" button is pressed
function connectButtonPressed() {
    var server = document.getElementById("serverInput").value;
    document.getElementById("connect").setAttribute("disabled", "disabled");
    setStatusBarText("Connecting to " + server + "...");

    // Connecting to server
    status = CONNECTING;

    try {
        socket = new WebSocket("ws://" + server); //Create a new socket
        socket.onopen = function () { //Handler to do when socket opens
            status = CONNECTED;
            onConnected(server);
        };
        socket.onmessage = function (srv_msg) { //Handler for messages received on the socket
            respondToMessage(srv_msg);
            //document.getElementById("server_msgs").textContent = srv_msg.data;
        };
        socket.onclose = function () { //Handler when socket closes, or is forced to close
            status = DISCONNECTED;
            onDisconnected();
        };
    } catch (exception) { //Failure to create a socket
        status = DISCONNECTED;
        onConnectionFailed();
    }
}

// Called when the "Disconnect" button is pressed
function disconnectButtonPressed() {
    document.getElementById("disconnect").setAttribute("disabled", "disabled");
    document.getElementById("login").setAttribute("disabled", "disabled");
    setStatusBarText("Disconnecting...");

    try { //Close the socket if any
        if (socket != null && socket.readyState == WebSocket.OPEN) {
            socket.close();
        }
        else { //Something to do if there is no socket
            addInfoMessage("Nothing to Disconnect");
            onDisconnected();
        }
    } catch (exception) { //Failed to close the socket
        status = DISCONNECTED;
        onConnectionFailed();
    }
}

// Called when the "Log in" button is pressed
function loginButtonPressed() {
    //Get login credentials
    var name = document.getElementById("nameInput").value;
    var password = document.getElementById("passwordInput").value;
    document.getElementById("login").setAttribute("disabled", "disabled");
    setStatusBarText("Authenticating..."); //Show status
    User_Name = name;
    // Authenticating

    if (status == CONNECTED) { //Send Login Credentials
        status = AUTHENTICATING;
        userId = getRandomInt();
        var data = "AUTH " + userId + "\r\n" + name + "\r\n" + password;
        try {
            loginTryCount++;
            socket.send(data);
        } catch (exception) {
            onLoginFailed();
        }
    } else { //Failure to send login credentials
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

    // Send Message
    if (status == AUTHENTICATED) {
        isSent = false;
        msgID = getRandomInt();
        var send_msg = "SEND " + msgID + "\r\n" + to + "\r\n" + message; //Creating the SEND message
        try {
            msgTryCount++;
            socket.send(send_msg);
            isSent = true;
            addChatMessage(msgID, User_Name, message, isSent);
        } catch (exception) { //Failure to send message
            isSent = false;
            addInfoMessage("Cannot Send Message, Use Pigeons");
        }

    }
    else {
        addInfoMessage("Client should be in Authenticated State to send messages"); //Just to complete the If tree
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
    onDisconnected(); //As suggested in the comment below
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
