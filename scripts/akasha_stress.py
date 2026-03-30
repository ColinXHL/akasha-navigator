import ctypes
import os
import random
import sys
import time
from dataclasses import dataclass
from datetime import datetime

import pyautogui
import pygetwindow as gw


@dataclass
class StressConfig:
    target_title_keyword: str = "AkashaNavigator"
    fast_forward_key: str = "6"
    test_hours: float = 6.0
    main_min: int = 20
    main_max: int = 40
    extra_min: int = 1
    extra_max: int = 9
    window_seconds: int = 120
    main_gap_min: float = 0.06
    main_gap_max: float = 0.16
    extra_gap_min: float = 0.12
    extra_gap_max: float = 0.26


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
LOG_FILE = os.path.join(SCRIPT_DIR, "stress_log.txt")


def log(message: str) -> None:
    line = f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {message}"
    print(line, flush=True)
    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(line + "\n")


def is_admin() -> bool:
    try:
        return ctypes.windll.shell32.IsUserAnAdmin() != 0
    except Exception:
        return False


def relaunch_as_admin() -> None:
    params = " ".join(f'"{arg}"' for arg in sys.argv)
    ctypes.windll.shell32.ShellExecuteW(None, "runas", sys.executable, params, None, 1)


def find_target_window(title_keyword: str):
    windows = gw.getWindowsWithTitle(title_keyword)
    for window in windows:
        if window.title and not window.isMinimized:
            return window
    return windows[0] if windows else None


def focus_window(title_keyword: str):
    window = find_target_window(title_keyword)
    if window is None:
        raise RuntimeError(f"Target window not found: {title_keyword}")

    try:
        if window.isMinimized:
            window.restore()
        window.activate()
    except Exception:
        pass

    time.sleep(0.08)
    return window


def tap_key_once(key_name: str) -> None:
    pyautogui.keyDown(key_name)
    time.sleep(0.01)
    pyautogui.keyUp(key_name)


def run_stress_test(config: StressConfig) -> None:
    pyautogui.PAUSE = 0
    pyautogui.FAILSAFE = True

    if not is_admin():
        print("Not running as administrator. Relaunching with elevation...")
        relaunch_as_admin()
        return

    log("=== Stress test started ===")
    log("Administrator privileges: yes")
    log(
        "Rule: every round send main random taps 20-40, then run a 2-minute window with extra random taps 1-9"
    )

    end_time = time.time() + config.test_hours * 3600
    round_index = 0

    while time.time() < end_time:
        round_index += 1
        focus_window(config.target_title_keyword)

        main_count = random.randint(config.main_min, config.main_max)
        log(f"Round {round_index}: main fast-forward taps = {main_count}")

        for _ in range(main_count):
            tap_key_once(config.fast_forward_key)
            time.sleep(random.uniform(config.main_gap_min, config.main_gap_max))

        log("Main burst finished. Entering 2-minute extra window")

        extra_count = random.randint(config.extra_min, config.extra_max)
        offsets = sorted(random.uniform(3, config.window_seconds - 3) for _ in range(extra_count))
        window_start = time.time()
        done = 0

        for offset in offsets:
            target = window_start + offset
            while time.time() < target and time.time() < end_time:
                time.sleep(0.02)

            if time.time() >= end_time:
                break

            focus_window(config.target_title_keyword)
            tap_key_once(config.fast_forward_key)
            done += 1
            log(f"Extra window tap {done}/{extra_count}")
            time.sleep(random.uniform(config.extra_gap_min, config.extra_gap_max))

        while time.time() - window_start < config.window_seconds and time.time() < end_time:
            time.sleep(0.05)

        log(f"Round {round_index} done. Extra taps executed: {done}/{extra_count}")

    log("=== Stress test finished ===")


if __name__ == "__main__":
    try:
        run_stress_test(StressConfig())
    except KeyboardInterrupt:
        log("Stopped by Ctrl+C")
    except Exception as ex:
        log(f"Stress test failed: {ex}")
        raise
