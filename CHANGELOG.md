# Changelog

## 0.6.0 — Unreleased

### Added

- `GET /api/notifications/tasks` to load the signed-in user's notification defaults and eligible task overrides in one response.
- `PUT /api/notifications/tasks` to update notification overrides for 1–500 owned tasks atomically.

### Changed

- Task notification responses support the frontend's effective alert states, filtering, one-click mute/enable and exact-setting Undo.
- Batch updates validate task ownership and notification lead times before saving any changes. Only the task creator can change shared House-task overrides, including when another user is an administrator.

### Compatibility

- Deploy with DoIt.Front `0.6.0` to use the new task controls.
- Existing individual task-notification endpoints remain available. Nullable values still inherit the user's defaults; explicit `false` disables an event without removing inbox activity.
- No additional EF Core migration or database reset is required for these changes.
