# Email provider & account lifecycle

How Dorado Cloud sends account email (verification + password reset) and how an
account moves through its lifecycle. Companion to
[moderation](moderation.md) and [backup/restore](backup-restore.md).

## Sending email

The app has one `IEmailSender` seam with two implementations:

| `Identity:Security:Email:Provider` | Sender | Use |
|---|---|---|
| `log` (default) | `LoggingEmailSender` | dev/small installs; messages go to the app log |
| `smtp` | `SmtpEmailSender` | production; requires `Smtp:Host` + `Smtp:From` |

`smtp` is selected only when the provider is `smtp` **and** the host and from
address are set; otherwise the app falls back to `log`. A failed send is logged
and swallowed, so a mail outage never blocks sign-up or password reset — check
the logs if a user reports a missing email.

### Configuration

| Key | Purpose | Default |
|---|---|---|
| `Identity:Security:Email:Provider` | `log` or `smtp` | `log` |
| `Identity:Security:Email:Smtp:Host` | SMTP server | _(empty)_ |
| `Identity:Security:Email:Smtp:Port` | SMTP port | `587` |
| `Identity:Security:Email:Smtp:UseStartTls` | STARTTLS | `true` |
| `Identity:Security:Email:Smtp:User` / `:Password` | credentials (blank ⇒ default network credentials) | _(empty)_ |
| `Identity:Security:Email:Smtp:From` / `:FromName` | envelope/display sender | `no-reply@dorado.local` / `Dorado Cloud` |
| `Identity:Security:PublicBaseUrl` | base URL for links in emails | `http://localhost:5080` |

Compose/`.env` equivalents: `EMAIL_PROVIDER`, `SMTP_HOST`, `SMTP_PORT`,
`SMTP_USE_STARTTLS`, `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`.

> `PublicBaseUrl` must be the URL users actually reach (scheme + host, no
> trailing path), or the verification/reset links will be wrong.

### Deliverability checklist

1. Send from a domain you control and set `Smtp:From` accordingly.
2. Publish **SPF** (authorize your SMTP host), **DKIM** (sign with the
   provider's key) and a **DMARC** policy.
3. Keep STARTTLS on (`UseStartTls=true`, port 587); avoid plaintext 25.
4. Warm up gradually and watch bounces — reset/verification links are the only
   mail, so complaints usually mean a misconfigured `From`.

## Account lifecycle

```
register ──► (verification email) ──► verify-email ──► verified
   │                                                    │
   └──────────────► login ──► consent ──► tokens ◄──────┘
                               │
                     forgot-password ──► reset-password
```

### Registration & login

- `POST /account/register` creates the account, sends a verification email, and
  signs in the browser cookie.
- `GET/POST /account/login` — all interactive forms are **antiforgery-protected**
  and `returnUrl` is restricted to same-site paths (no open redirects).
- Email verification is **not enforced by default** (`RequireEmailVerification=false`)
  so a deployment without mail still works. Set it to `true` once SMTP is live if
  you want to gate sign-in on a verified address.

### Email verification

- `GET /account/verify-email?token=…` consumes a single-use token and sets
  `Account.EmailVerified`.
- Tokens are stored **hashed** (SHA-256) with `EmailVerificationTtlHours` (24).

### Password reset

- `POST /account/forgot-password` always returns the same generic response (no
  account enumeration) and, if the account exists, emails a link.
- `GET /account/reset-password?token=…` renders the form; `POST` consumes the
  token and sets the new password (minimum 8 characters).
- Tokens are single-use, hashed, and expire after `PasswordResetTtlHours` (2).

### Consent

The first time a client requests scopes, `/connect/authorize` shows a consent
screen; the grant (account + client + normalized scopes) is stored in
`ConsentGrant`. Re-consent is required if the scope set changes.

### Data lifecycle (GDPR)

| Action | Endpoint | Effect |
|---|---|---|
| Export | `GET /v1/identity/me/export` | JSON dump of account, devices, settings, profile, activities, badges, follows, inbox messages |
| Erase | `DELETE /v1/identity/me` | Deletes the account and associated data and revokes OIDC tokens/authorizations; inbox messages the account sent are removed |

Account tokens and consent grants are deleted with the account.

## Troubleshooting

| Symptom | Check |
|---|---|
| No email arrives, provider=`log` | expected — the message is in the app log; set `Provider=smtp` |
| No email arrives, provider=`smtp` | app logs `Failed to send account email to …`; verify host/port/credentials and that `From` is allowed by SPF |
| "verification link invalid or expired" | link already used (single-use) or past `EmailVerificationTtlHours`; re-register or resend |
| "reset link invalid or expired" | token used or past `PasswordResetTtlHours`; request a new one |
| Users can sign in without verifying | by design; set `RequireEmailVerification=true` |
| Links point at the wrong host | fix `Identity:Security:PublicBaseUrl` |
| `400 invalid request` on a form POST | antiforgery token missing/stale — reload the page |

## Security notes

- Tokens are opaque, random, **hashed at rest**, single-use, and time-limited.
- Interactive POSTs require an antiforgery token; redirects are local-only.
- The SMTP password is configuration — keep it in a secret manager, not the repo.
- Prefer the logging sender in development so no real mail is sent by tests.
