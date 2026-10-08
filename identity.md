# Identity and Authentication

Status: Draft v0.1. Owner: Developer 2. Values marked (proposed) are defaults to confirm; all are configurable through Options or `SystemSetting`.

## 1. Principles

- `ApplicationUser` (ASP.NET Core Identity, `IdentityUser<Guid>`) is a login account. It is not a Student, Employee or Applicant.
- A `PersonProfile` may exist without an account (`UserId` is nullable). Link account to person through `PersonProfile.UserId`.
- Authentication proves who the caller is. What they may do is decided by `authorization.md`.
- Every failure is a `Result`. No exceptions thrown by our code.

## 2. Entities

**ApplicationUser**: `UserName`, `Email`, `PhoneNumber?`, `IsActive`, `EmailConfirmed`, `LastLoginAt?`, `CreatedAt`, `UpdatedAt?`, plus Identity fields (password hash, security stamp, lockout).

**RefreshToken**: `UserId`, `TokenHash`, `ExpiresAt`, `CreatedAt`, `RevokedAt?`, `ReplacedByTokenId?`, `CreatedByIp?`, `RevokedByIp?`.

**PasswordResetOtp**: `Id`, `UserId`, `OtpHash`, `ExpiresAt`, `AttemptCount`, `MaxAttempts`, `ConsumedAt?`, `CreatedAt`, `RequestedByIp?`, `ResendAvailableAt`.

## 3. Settings (proposed defaults)

| Setting | Value |
|---|---|
| Access token lifetime | 15 minutes |
| Refresh token lifetime | 7 days (absolute, rotating) |
| Clock skew | 30 seconds |
| Lockout | 5 failed attempts, 15 minutes |
| Password policy | min 10 characters, upper, lower, digit, symbol |
| OTP length / expiry / max attempts | 6 digits / 10 minutes / 5 |
| OTP resend cooldown | 60 seconds |
| OTP requests per hour | 5 per user, 10 per IP |
| Reset token lifetime | 10 minutes, single use |

## 4. Endpoints

| Method and route | Auth | Purpose |
|---|---|---|
| `POST /api/v1/auth/login` | anonymous | email/username and password, returns access and refresh tokens |
| `POST /api/v1/auth/refresh` | anonymous | exchange a refresh token for a new pair |
| `POST /api/v1/auth/logout` | authenticated | revoke the current refresh token |
| `POST /api/v1/auth/forgot-password` | anonymous | request an OTP by email |
| `POST /api/v1/auth/verify-otp` | anonymous | verify the OTP, returns a reset token |
| `POST /api/v1/auth/reset-password` | anonymous | set a new password with the reset token |
| `POST /api/v1/auth/change-password` | authenticated | change password with the current one |

## 5. Login

1. Find the user. Unknown user, wrong password and inactive account all return the same `INVALID_CREDENTIALS` (`ErrorType.InvalidCredentials`, 401) so callers cannot tell which one failed.
2. If locked out, return `ACCOUNT_LOCKED_OUT` (403).
3. On success: reset the failure counter, set `LastLoginAt`, issue the access token and a refresh token, audit `AUTH_LOGIN_SUCCEEDED`.
4. On failure: increment the failure counter (Identity lockout), audit `AUTH_LOGIN_FAILED` (never the password).

## 6. Tokens

- **Access token**: JWT, signed with a symmetric key of at least 32 bytes from configuration (never in code). Claims: `sub` (user id), `jti`, `person_id` (when linked), role claims. Permissions and scopes are not embedded in the token; they are resolved server side per request (see `authorization.md`).
- Validation: signature, issuer, audience, lifetime, small clock skew.
- **Refresh token**: 64 random bytes (cryptographic RNG), returned once as Base64Url, stored only as a SHA-256 hash.
- **Rotation**: each refresh revokes the used token (`RevokedAt`, `ReplacedByTokenId`) and issues a new one. Record IPs.
- **Reuse detection**: presenting an already-revoked token revokes every active refresh token of that user and returns `REFRESH_TOKEN_REUSED`. Audit `AUTH_TOKEN_REUSE_DETECTED`.
- **Logout** revokes the presented token. Deactivating a user, resetting a password or changing roles revokes all their refresh tokens.
- Expired or unknown tokens return `REFRESH_TOKEN_INVALID` / `REFRESH_TOKEN_EXPIRED` (401).

## 7. Forgot password (OTP over SMTP)

### Step 1: `forgot-password` { email }

- Always returns the same generic success response and takes a similar time, whether or not the account exists. Never reveal existence.
- If the user exists and is active:
  1. Invalidate previous unconsumed OTPs for that user.
  2. Enforce the resend cooldown and the hourly caps. If exceeded, do nothing visible (same generic response) and audit it.
  3. Generate a 6-digit OTP with `RandomNumberGenerator` (never `Random`).
  4. Store only `HMAC-SHA256(serverSecret, userId + ":" + otp)`. Set `ExpiresAt`, `MaxAttempts`, `ResendAvailableAt`, `RequestedByIp`.
  5. Send the email through `IEmailSender` (HTML + plain text). Email failures are Results (`EXTERNAL_SERVICE_UNAVAILABLE` internally), logged, and never change the public response.
- The OTP is never logged, never returned, never stored in plain text.

### Step 2: `verify-otp` { email, otp }

- Validate: OTP exists, not consumed, not expired, attempts below max.
- Compare hashes with `CryptographicOperations.FixedTimeEquals`.
- Wrong OTP: increment `AttemptCount`. At `MaxAttempts` the OTP is locked (`OTP_ATTEMPTS_EXCEEDED`).
- Success: mark the OTP verified and return a short-lived, single-use, purpose-bound reset token (not the OTP). Implementation: Identity's password reset token provider, which is bound to the user and invalidated when the password or security stamp changes.
- Errors for this step: `OTP_INVALID`, `OTP_EXPIRED`, `OTP_ATTEMPTS_EXCEEDED`. They must not reveal whether the account exists (an unknown email behaves like an invalid OTP).

### Step 3: `reset-password` { email, resetToken, newPassword, confirmPassword }

1. Validate token, password policy and that the two passwords match (`PASSWORD_MISMATCH`, `PASSWORD_POLICY_VIOLATION`, `RESET_TOKEN_INVALID`).
2. Reset the password through Identity.
3. Mark the OTP consumed, revoke all refresh tokens of the user, reset lockout counters.
4. Audit `AUTH_PASSWORD_RESET_COMPLETED`.
5. Send the "your password was changed" confirmation email.

### Emails

Templates (HTML and text): OTP email (app name, OTP, expiry minutes, "ignore if this was not you"), password changed, welcome (account created, with set-password flow). SMTP settings (`SmtpOptions`: host, port, SSL/STARTTLS, username, password, from name, from address) come from configuration and are validated at startup. Locally Mailpit receives all mail (SMTP 1025, UI http://localhost:8025).

## 8. Error codes

`INVALID_CREDENTIALS`, `ACCOUNT_LOCKED_OUT`, `ACCOUNT_INACTIVE`, `REFRESH_TOKEN_INVALID`, `REFRESH_TOKEN_EXPIRED`, `REFRESH_TOKEN_REUSED`, `OTP_INVALID`, `OTP_EXPIRED`, `OTP_ATTEMPTS_EXCEEDED`, `OTP_RESEND_TOO_SOON` (internal only), `RESET_TOKEN_INVALID`, `PASSWORD_MISMATCH`, `PASSWORD_POLICY_VIOLATION`, `EXTERNAL_SERVICE_UNAVAILABLE`.

## 9. Audit events

`AUTH_LOGIN_SUCCEEDED`, `AUTH_LOGIN_FAILED`, `AUTH_ACCOUNT_LOCKED`, `AUTH_LOGOUT`, `AUTH_TOKEN_REFRESHED`, `AUTH_TOKEN_REUSE_DETECTED`, `AUTH_PASSWORD_RESET_REQUESTED`, `AUTH_OTP_VERIFIED`, `AUTH_OTP_ATTEMPTS_EXCEEDED`, `AUTH_PASSWORD_RESET_COMPLETED`, `AUTH_PASSWORD_CHANGED`. Include actor, IP, user agent, CorrelationId. Never include passwords, tokens or OTPs.

## 10. Cleanup jobs

Delete expired refresh tokens and expired or consumed OTPs after a retention period (proposed: 30 days). Runs as a Hangfire recurring job and returns a Result.

## 11. Tests

Login success/failure/lockout, refresh rotation and reuse detection, logout, OTP generation and hashing, expiry (with `FakeClock`), attempts limit, resend cooldown, generic responses for unknown emails, reset flow end to end (reading the OTP from Mailpit or a fake `IEmailSender`), all refresh tokens revoked after reset.