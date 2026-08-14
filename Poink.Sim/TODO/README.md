# TODO

TODO List for Poink.SIM project

## Standards

  | Status | Meaning |
  |---|---||
  | `In Progress` | Agent is actively working on it. |
  | `Pending Review` | Work is done; User to test it and comment. |
  | `Done` | Completed. |

  Structure
  Index Subject | Scope
  - Status
  - Description

# TODO LIST

RISK, NOTE severity — this interface is wide (45 members). It's a faithful mirror of the concrete surface, so it satisfies your inspection goal, but the breadth signals the class carries several responsibilities (run lifecycle, brain building, model persistence, live-episode telemetry). FINDING: RlTrainingService is a candidate for later splitting into narrower services. RECOMMEND: leave as-is now since you asked for a straight interface extraction; revisit if you want the surface segmented. Your call — no action taken.



Robustness choices CTrader Tokens
Expired tokens are skipped. TryRestoreAsync checks token.ExpiresIn (an absolute timestamp) and, since there's no refresh-token flow yet, logs a warning and stays disconnected rather than attempting a doomed connect.
Persistence is best-effort. SaveTokenAsync/DeleteTokenAsync swallow-and-log DB failures so a database hiccup never breaks the live login/logout.
Restore degrades gracefully. Any failure (revoked token, cTrader offline) → disconnected + warning, never a crash.
Not run (rule 10.1) — build-verified only. One thing to confirm on your next launch: the log line cTrader: restoring saved session... should appear on startup if a token is stored, followed by the connect logs.

Known gap (unchanged)
No refresh-token flow — when the access token eventually expires, startup restore skips it and you log in again.
