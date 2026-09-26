# Session refinement implementation status

Local implementation only. No new release published; existing rc3 outputs remain intact.

Windows now prepares native INPUT arrays and immutable absolute timelines before Ready, arms emergency stop, uses a high-resolution waitable timer and stops unsafe overdue playback. Macro edits/display-metric changes invalidate preparation. Recorded timestamps never shift to compensate for scheduler delays.

Windows has decimal-only speed editing, per-task JSON speed/loop profiles, captured modifier shortcuts, conflict rollback and individual reset. Mac equivalents remain incomplete.

New ASP.NET Core WebSocket server and Windows session screen implement account-free installation credentials, editable Public IDs, approved invitations/codes, Host/Member permissions, locking, Ready/task/ping/jitter display, host transfer/election, bounded command queues, sequence rejection, continuous clock sampling, quality-gated common start timestamps and optional scheduled starts. Each client plays only its own prepared macro. Credentials are derived per server; installation secrets use Windows DPAPI.

Validation: Windows/server builds pass; 13 timeline checks, 21 UI checks, 14 Classic fixture checks, 26 server checks and 11 desktop client checks. Session checks are loopback only. CodeRabbit first review raised six issues: five fixed; timeline-retiming suggestion rejected because it conflicts with immutable scheduling. Follow-up review raised zero issues. Reports: tests/implementation-dev.

Unfinished: public hosting/authoritative global Public ID service; reconnect grace/state recovery; safe-loop desync rejoining; Nearby/LAN discovery and transport fallback; trusted-device features; remaining local editor/library/settings; Mac implementation/build/acceptance. Fixed quality/lateness thresholds are conservative guards, not measured cross-device guarantees. Input isolation and Roblox compatibility remain experimental. Unsigned driver prototypes are not installed. Preserve emergency local stop and explicit consent for session control.

Use COMPLETE-REQUIREMENTS-AUDIT-2026-09-25.md for remaining requirements. Do not publish until separately authorized.
