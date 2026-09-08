# AI Agent Instructions (repository-wide)

These rules apply to **all** AI agents that create or modify changes in this repository.

## Mandatory documentation requirement

1. For **every functional or technical code change**, the corresponding documentation in the `/docs` subfolder must be created or updated.
2. A pull request or commit containing code changes is **incomplete** if it does not include a matching documentation update in `/docs`.
3. Documentation-only changes are exempt (when no code has been changed).

## Audience and focus of documentation in `/docs`

1. Documentation in the `/docs` folder is primarily a **guide for users of the SAF framework**.
2. Write content so users can correctly apply the framework (e.g., configuration, integration, usage, behavior, limitations).
3. Avoid internal notes without user relevance, or clearly mark them as internal.

## Minimum required content for relevant changes

If a change affects SAF behavior, configuration, or usage, the documentation in `/docs` must include at least:

1. What was changed?
2. Why is the change relevant for SAF users?
3. How do users apply or use the new/updated functionality?
4. Are there breaking changes, migration steps, or special notes?

## Quality requirement

Documentation in `/docs` must be consistent, unambiguous, and directly actionable for SAF users.
