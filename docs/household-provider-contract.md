# Household Provider Contract

`GET /api/integrations/household/v1/now` accepts optional `date=YYYY-MM-DD` and optional `timeZoneId` query values under the `tasks.read` scope. `timeZoneId` may be an IANA or Windows identifier supported by .NET. Invalid identifiers return `400 { "code": "invalid_time_zone", "message": "Time zone is invalid." }`.

When `date` is omitted, DoIt derives the response date from the current instant in `timeZoneId`; when both values are omitted it uses UTC. An explicit date is never shifted by the requested timezone.

Every task item preserves `recurrenceType`, `assignmentMode`, `assigneeNames`, `timeZoneId`, and the nullable UTC `completedAt` timestamp. Occurrence actions return `occurrenceId`, `taskId`, `occurrenceDate`, and `occurrenceStatus`.

Integration access remains bound to the connected DoIt user. An admin's integration token does not gain normal DoIt admin visibility or action rights over another user's exclusively assigned task.
