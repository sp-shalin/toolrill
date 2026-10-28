# Security Policy

## Private Vulnerability Reports

Do not disclose vulnerabilities in public issues, pull requests, or discussions.
On GitHub, use the repository's **Security > Advisories > Report a vulnerability**
option when private vulnerability reporting is enabled. If that option is
unavailable, contact the repository owner privately using contact details on
their GitHub profile to arrange a private reporting channel before sharing
sensitive details.

Include the affected version, environment, reproduction steps or a minimal
proof of concept, expected impact, and any suggested mitigation. Remove
credentials, tokens, and private data. Coordinate public disclosure with the
maintainers after a fix or mitigation is available.

## Security Boundary

In OSS ToolRill, named hosts and tool filters are configuration selectors, not
authentication or multi-tenant isolation boundaries. Do not use them to isolate
mutually untrusted clients or tenants. Built-in inbound authentication and
multi-tenant isolation are outside the core gateway's scope.

Bind HTTP hosts to loopback (`127.0.0.1`) or place them behind an authenticated
reverse proxy in shared environments. Configure downstream credentials and
permissions with least privilege; tool filtering is not a substitute for
downstream authorization.