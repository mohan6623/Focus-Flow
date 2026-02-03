---
description: Update bugs.md when a bug is fixed or discovered
---
# Auto-Update bugs.md Workflow

When you encounter a new bug OR fix an existing one, you **MUST** update `d:\FocuseFlow\ScreenTimeTracker\bugs.md`.

## When to Update
1. **Bug Discovered**: You find a problem or the user reports one.
2. **Bug Fixed**: You successfully implement a fix.

## How to Update

### 1. Adding a Pending Issue (Discovery)
Add a new item to the "Pending / Known Issues" section:
```markdown
- [ ] **Issue Name**: Brief description of the problem.
```

### 2. resolving an Issue (Fix)
Move the issue from "Pending" to the "Resolved Issues" table at the top:

| Key | Issue Description | Resolution | Fixed Date |
|-----|-------------------|------------|------------|
| **New-ID** | **Issue Name**<br>Description. | Brief explanation of the fix (e.g., "Updated regex", "Added null check"). | YYYY-MM-DD |

## Guidelines
- **Be Brief**: The description and resolution should be concise.
- **Date It**: Always include the date the fix was applied.
- **ID System**: Use a simple prefix like `UI-xx`, `Data-xx`, or just incrementing numbers if preferred.
