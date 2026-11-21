#!/usr/bin/env python3
"""Run Release tests and package verification, retaining full local logs."""
import argparse
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--filter", help="Run only matching tests, without packing.")
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
logs = root / "artifacts" / "logs"
logs.mkdir(parents=True, exist_ok=True)
active = None


def cancel(signum, _frame):
    if active is not None:
        try:
            os.killpg(active.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
    raise KeyboardInterrupt


signal.signal(signal.SIGINT, cancel)
signal.signal(signal.SIGTERM, cancel)
steps = [("tests", ["dotnet", "test", "Lib.GAB.Tests/Lib.GAB.Tests.csproj", "-c", "Release"])]
if args.filter:
    steps[0][1].extend(["--filter", args.filter])
else:
    steps.append(("pack", ["dotnet", "pack", "Lib.GAB/Lib.GAB.csproj", "-c", "Release"]))

for step, command in steps:
    with tempfile.NamedTemporaryFile(mode="w", prefix=f"{step}-", suffix=".log", dir=logs, delete=False) as log:
        log_path = Path(log.name)
        try:
            active = subprocess.Popen(command, cwd=root, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            result = active.wait()
        except KeyboardInterrupt:
            if active is not None:
                try:
                    active.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    os.killpg(active.pid, signal.SIGKILL)
                    active.wait()
            print(f"{step} cancelled. Log: {log_path}", file=sys.stderr)
            sys.exit(130)
        except OSError as error:
            print(f"{step} failed: {error}. Log: {log_path}", file=sys.stderr)
            sys.exit(1)
        finally:
            active = None
    if result == 0 and step == "tests" and "Passed!" not in log_path.read_text(errors="replace"):
        print(f"tests failed: no passing test run was reported. Log: {log_path}", file=sys.stderr)
        sys.exit(1)
    if result:
        lines = log_path.read_text(errors="replace").splitlines()
        diagnostics = [line for line in lines if any(word in line.lower() for word in ("error", "failed", "exception", "timeout"))]
        print(f"{step} failed ({result}). Log: {log_path}", file=sys.stderr)
        print("\n".join((diagnostics or lines)[-12:]), file=sys.stderr)
        sys.exit(result if result > 0 else 1)
print("ok")
