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

1. Move Charts into a resizeable card that will fill screen. Simulation, Trainer, Profile pages | UI
- TODO
- For Simulation and profile there are charts built that has `DrawableOhlcvChart`, Volume Profile, Time Profile, and Chart overlay.
It is fine for it to be a seperate components but there should be a control to maximize these elements to fill the screen.
For simulation the Profile Bars slider should be included.

2. Collapsable Simulation inputs. | Simulation | UI
- TODO
- A collapse button should be placed left of the Play button that would collapse the Data Range selector and the brain/Trader options

3. Brain builder settings retention | Training | UI
-TODO
- I need a way to retain, just in current session memory, a configured brains settings. If it is retained, then I need a way to load them
to prepopulate the configs. Everytime I go back I need to reset the settings which is anoying. Especially when I am trying to tune

4. Validation loop 
- TODO
- Nothing today distinguishes learning from memorizing. Add to RlTrainingService: every N episodes, run a handful of evaluation episodes on a held-out date range (later than the training range, never sampled by RandomEpisodeWindow), with greedy action selection (argmax, no sampling) and no weight update. Track validation reward/equity separately in the Live tab, and treat "best validation score" as the snapshot worth saving — your snapshot-per-save history in IBrainRepository already supports keeping several and picking the winner.



