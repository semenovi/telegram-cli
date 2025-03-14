# Telegram Command Line Client

A non-interactive command-line client for sending Telegram messages.

## Overview

This application allows you to send Telegram messages from the command line without entering an interactive mode. It's designed for use in scripts and automation, returning exit codes to indicate the result of operations.

## Installation

1. Download the compiled executable or build from source.
2. Obtain your Telegram API credentials from https://my.telegram.org/apps.

## Usage

```
telegram-cli.exe -a API_ID -h API_HASH -p +PHONE_NUMBER -t @USERNAME -m "Your message"
```

### Required Parameters

- `-a, --apiId` - Your Telegram API ID (numeric)
- `-h, --apiHash` - Your Telegram API Hash (string)
- `-p, --phone` - Your phone number in international format (e.g., +12345678901)
- `-t, --target` - Target username (@username) or phone number
- `-m, --message` - Text message to send

### Optional Parameters

- `-c, --code` - Authorization code (required for first login)
- `-w, --password` - Two-factor authentication password (if enabled)
- `-s, --sessionFile` - Path to session file (default: "telegram_session.dat")

## First-Time Authorization

When you first run the client, you will need to authenticate:

1. **First run**: The program will exit with code 2, indicating a verification code is needed
   ```
   telegram-cli.exe -a API_ID -h API_HASH -p +PHONE_NUMBER -t @USERNAME -m "Message"
   ```
   
2. **Second run**: Add the verification code that was sent to your Telegram account
   ```
   telegram-cli.exe -a API_ID -h API_HASH -p +PHONE_NUMBER -c VERIFICATION_CODE -t @USERNAME -m "Message" 
   ```
   
3. **If 2FA is enabled**: Add your password
   ```
   telegram-cli.exe -a API_ID -h API_HASH -p +PHONE_NUMBER -c VERIFICATION_CODE -w PASSWORD -t @USERNAME -m "Message"
   ```

## Exit Codes

The program returns the following exit codes:

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | Authorization required |
| 2 | Verification code required |
| 3 | Two-factor authentication password required |
| 4 | Message sending failed |
| 5 | Connection failed |
| 6 | Target user/chat not found |
| 7 | Unknown error |
| 10 | Session file error |

## Session Management

The application saves your authentication data in a session file. If you experience authentication issues, try deleting the session file and logging in again.

## Examples

Sending a message after successful authentication:
```
telegram-cli.exe -a 12345 -h abcdef1234567890 -p +12345678901 -t @username -m "Hello from CLI!"
```

## Requirements

- .NET Runtime
- Internet connection
- Valid Telegram account

## Security Notes

- Never share your API ID, API Hash, or session file
- This client authenticates as your Telegram account, not as a bot
- All messages will appear as being sent by you
