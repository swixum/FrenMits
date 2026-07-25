"""Where the dev tools find FFLogs API credentials.

Dev-only, like the rest of tools/ - never shipped in the plugin DLL.

The plugin's own config stores the secret DPAPI-encrypted (FflogsClientSecretEnc)
from v23 on, which these scripts can't decrypt. So the credentials live in a
small file of their own, outside the repo so it can never be committed:

    ~/.config/frenmits/fflogs.json      (chmod 600)

    {
      "client_id":     "...",
      "client_secret": "..."
    }

Resolution order, first hit wins:
    1. --id / --secret on the command line
    2. --creds <FrenMits.json>          (only if the secret is still plaintext)
    3. ~/.config/frenmits/fflogs.json
    4. FFLOGS_CLIENT_ID / FFLOGS_CLIENT_SECRET in the environment
"""

import json
import os
import sys

CREDS_FILE = os.path.expanduser("~/.config/frenmits/fflogs.json")


def _from_local_file():
    if not os.path.exists(CREDS_FILE):
        return None, None
    try:
        with open(CREDS_FILE, "r", encoding="utf-8-sig") as fh:
            d = json.load(fh)
    except Exception as e:
        print(f"note: {CREDS_FILE} could not be read ({e}).", file=sys.stderr)
        return None, None
    # Accept either the plain names or the plugin config's spelling.
    cid = d.get("client_id") or d.get("FflogsClientId")
    secret = d.get("client_secret") or d.get("FflogsClientSecret")
    return cid, secret


def resolve(cid=None, secret=None, creds_path=None):
    """Return (client_id, client_secret), or exit with a useful message."""
    if (not cid or not secret) and creds_path:
        try:
            with open(creds_path, "r", encoding="utf-8-sig") as fh:
                cfg = json.load(fh)
        except Exception as e:
            sys.exit(f"Could not read {creds_path}: {e}")
        cid = cid or cfg.get("FflogsClientId")
        secret = secret or cfg.get("FflogsClientSecret")
        if not secret and cfg.get("FflogsClientSecretEnc"):
            print(f"note: that config stores the secret encrypted; using {CREDS_FILE} instead.",
                  file=sys.stderr)

    if not cid or not secret:
        fcid, fsecret = _from_local_file()
        cid = cid or fcid
        secret = secret or fsecret

    cid = cid or os.environ.get("FFLOGS_CLIENT_ID")
    secret = secret or os.environ.get("FFLOGS_CLIENT_SECRET")

    if not cid or not secret:
        sys.exit(
            "No FFLogs credentials.\n"
            f"  Put them in {CREDS_FILE} as "
            '{"client_id": "...", "client_secret": "..."}\n'
            "  or pass --id/--secret, or set FFLOGS_CLIENT_ID / FFLOGS_CLIENT_SECRET."
        )
    return cid, secret
