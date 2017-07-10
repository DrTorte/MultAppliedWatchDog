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

```
emailAlertThreshold: The amount of polls that the leg has to be detected down for before sending an email. Setting this to something higher than 1 allows a bit of tolerance to intentional reboots, but reduces accuracy.

Each leg is tracked individually. Once a leg is back up, each poll it remains online for reduces this increment. Once it reaches 0, it is deemed stable once more and the email alert can be triggered again. Until it has reached 0, there will not be an additional email

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
