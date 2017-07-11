# MultAppliedWatchdog
A watcher service that scans for MultApplied legs going up/down.

## Getting Started

Simply compile, update the configuration file, and run. You're good to go!

### Prerequisites

.Net Framework 4.5.1 is required.

### Using

Once the configuration is complete and you run the application, it runs in the background and processes its tasks without much need for human input, unless connectivity is lost.

### Configuring

Open the .config file and fill in the information as follows:

#### Core:
```
server: URL of the server, including the API Directory.

port: NYI, but specify port.

username: The MultApplied username you wish to use.

password: Associated password.

refreshTimer: The intervals of server polls, in milliseconds. Default is once a minute.
```

#### Email:

As part of the watch, it also sends emails based up on the alert threshold that is entered. This threshold refers to how many polls the customer has to remain down for during the polling. For example, if the threshold is five and the refresh timer is 60 seconds, it must remain down for five polls. 

The increment is reduced if the leg is up when it is polled once more. So, if it is currently 3, but it is polled and is online, it is back down to 2. Once it has reached the threshold, an email is sent to all listed emails. This way, short interruptions such as intentional reboots or minor power surges do not cause a surge of emails.

Once an email has been sent, this increment counter has to reach 0 once more before it is no longer considered tripped. Once it is back to 0 (using the above example, 5 polls that the bonder was online for) an email can once more be sent.

```
emailAlertThreshold: The amount of polls that the leg has to be detected down for before sending an email.

fromEmail: The email that you wish to use in the from field.

toEmails: A list of all emails to send to. Seperate by a comma (,).

ccEmails: A list of all emails to add to CC. seperate by a comma (,).

smtp: SMTP server to use.

smtpPort: Port of the SMTP server.

smtpUser: The user to log into the SMTP server with, if required. Check with your server provider if you're not sure.

smtpPass: Password to log into the SMTP server with, if required.
```

## License

Licensed under the MIT License.

## Authors

Kristofer Svärdstål (https://www.drtorte.net)
