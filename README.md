# stardew-valley-host-bot
Turn the host into a bot.

# Commands

- `hostbot sleep`: the host will sleep immediately.
- `hostbot sleep no`: the host will NOT sleep automatically.
- `hostbot sleep HHMM`: the host will auto sleep after time `HHMM`.
  - `HHMM` is the 24-hour notation of a time. `HH` are hours like `07`, `16`. `MM` are minutes like `30`, `50`.
  - The range is from `0600` (which means 6:00), to `2600` (which means 2:00 AM)
  - For example, `hostbot sleep 2200` will let the host sleep at 10 PM.

Also, 
- when there are remote players
  - the auto-sleep will be automatically on.
  - the world will be resumed if it's paused.
- where there is no remote player
  - the auto-sleep will be automatically off
  - the world will be paused

# 星露谷机器主持人
把主持人变成自动机器人。

# 命令

- `hostbot sleep`: 主持人会立马回屋子睡觉。
- `hostbot sleep no`: 主持人会失去自动睡觉功能。
- `hostbot sleep HHMM`: 主持人会在时间 `HHMM` 后自动回屋子睡觉
  - `HHMM` 24 小时时间，前两位是小时，后两位是分钟.
  - 范围是从 `0600` (早上 6 点), 到 `2600` (凌晨 2 点)
  - 举例：`hostbot sleep 2200` 会让主持人在晚上 10 点自动回屋睡觉

另外，
- 当有远程玩家的时候
  - 主持人的自动睡觉功能会自动打开。
  - 如果世界被暂停的话，会自动继续运转。
- 当没有远程玩家的时候
  - 主持人的自动睡觉功能会被取消。
  - 世界会被暂停。
