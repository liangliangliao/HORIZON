"""Run the separate native APK on an Android device and collect normal frames."""
import io
import json
from pathlib import Path
import subprocess
import sys
import tarfile
import time

PACKAGE = "com.liangliangliao.horizon.smoke"
DEVICE_PATH = f"/sdcard/Android/data/{PACKAGE}/files/android-smoke"
OUTPUT = Path("artifacts/android-smoke/device")
DEVICE_PREFIX = ("run-as", PACKAGE)


def adb(*args, check=True):
    return subprocess.run(["adb", *args], check=check, capture_output=True)


def collect():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    logs = adb("logcat", "-d", "-s", "Unity", "AndroidRuntime", check=False)
    (OUTPUT / "logcat.txt").write_bytes(logs.stdout)
    screen = adb("exec-out", "screencap", "-p", check=False)
    if screen.returncode == 0 and screen.stdout.startswith(b"\x89PNG\r\n\x1a\n"):
        (OUTPUT / "device-final-screen.png").write_bytes(screen.stdout)
    packed = adb("exec-out", *DEVICE_PREFIX, "tar", "-cf", "-", "-C", DEVICE_PATH, ".", check=False)
    if packed.returncode == 0:
        with tarfile.open(fileobj=io.BytesIO(packed.stdout)) as archive:
            for member in archive.getmembers():
                path = Path(member.name)
                if member.isfile() and not path.is_absolute() and ".." not in path.parts:
                    data = archive.extractfile(member)
                    (OUTPUT / path.name).write_bytes(data.read())
    else:
        print("Could not collect native report: " + packed.stderr.decode(errors="replace")[:1000], flush=True)


def main():
    global DEVICE_PREFIX
    apk = Path(sys.argv[1] if len(sys.argv) > 1 else "artifacts/android-smoke/HORIZON-Smoke.apk")
    assert apk.is_file(), "Native smoke APK is missing"
    # Fail before launching if the host output directory is not writable.
    OUTPUT.mkdir(parents=True, exist_ok=True)
    # run-as can lack access to Android 10's emulated external storage. The
    # disposable Google APIs emulator supports root adbd; the app still runs
    # with its normal UID and permissions.
    adb("root", check=False)
    adb("wait-for-device")
    if adb("shell", "id", "-u").stdout.strip() == b"0":
        DEVICE_PREFIX = ()
    adb("install", "-r", str(apk))
    resolved = adb("shell", "cmd", "package", "resolve-activity", "--brief", PACKAGE).stdout.decode().splitlines()
    activity = next((line.strip() for line in reversed(resolved) if "/" in line), None)
    assert activity, "Could not resolve the smoke APK launcher"
    adb("shell", "wm", "size", "1080x2400")
    adb("shell", "am", "force-stop", PACKAGE)
    adb("logcat", "-c")
    adb("shell", "am", "start", "-W", "-n", activity, "--ez", "horizonSmoke", "true")
    deadline = time.monotonic() + 240
    backgrounded = False
    back_pressed = False
    previous = None
    previous_read_error = None
    try:
        while time.monotonic() < deadline:
            read = adb("shell", *DEVICE_PREFIX, "cat", f"{DEVICE_PATH}/report.json", check=False)
            if read.returncode:
                message = read.stderr.decode(errors="replace").strip()[:1000]
                if message != previous_read_error:
                    print("Native report read: " + message, flush=True)
                    previous_read_error = message
                time.sleep(2)
                continue
            try:
                report = json.loads(read.stdout)
            except (ValueError, UnicodeDecodeError):
                time.sleep(2)
                continue
            status = report["status"]
            if status != previous:
                print(f"Native Android smoke: {status}, {len(report.get('frames', []))} frames", flush=True)
                previous = status
            if status == "failed":
                raise AssertionError(report.get("error") or report.get("errors"))
            if status == "await_background" and not backgrounded:
                adb("shell", "input", "keyevent", "KEYCODE_HOME")
                time.sleep(2)
                adb("shell", "am", "start", "-W", "-n", activity, "--activity-single-top")
                backgrounded = True
            if status == "await_back" and not back_pressed:
                adb("shell", "input", "keyevent", "KEYCODE_BACK")
                back_pressed = True
            if status == "passed":
                assert backgrounded, "The native background/foreground check was skipped"
                assert back_pressed, "The native reward system-back check was skipped"
                assert not report.get("errors"), "Unity reported runtime errors"
                assert report["graphics"] == "OpenGLES3", "The tested Android backend differs from the preview"
                return
            time.sleep(2)
        process = adb("shell", "pidof", PACKAGE, check=False).stdout.decode(errors="replace").strip()
        raise AssertionError("Native Android flow timed out; app PID=" + (process or "missing") +
                             "; last report read=" + (previous_read_error or "no read error") +
                             "; inspect the collected Unity logcat")
    finally:
        collect()


if __name__ == "__main__":
    main()
