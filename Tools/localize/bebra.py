import os
import re
import shutil
from datetime import datetime
from pathlib import Path
import sys

BASE = Path(r"C:\Users\MSI\Desktop\Space Station 14")
ENGLISH_LOCALE = BASE / "Persistence" / "Resources" / "Locale" / "en-US"
RUSSIAN_LOCALE = BASE / "Persistence" / "Resources" / "Locale" / "ru-RU"
BACKUP_DIR = BASE / "ru_locale_backup"
LOG_FILE = BASE / "locale_cleanup.log"
REPORT_FILE = BASE / "locale_cleanup_report.txt"
UNTRANSLATED_LOG = BASE / "untranslated_files.log"

# Папки для игнорирования
IGNORE_DIRS = {"ss14-ru"}

HAS_CYRILLIC = re.compile(r"[а-яёА-ЯЁ]")

def print_progress(current, total, prefix="", suffix="", bar_length=50):
    """Выводит прогресс-бар."""
    if total == 0:
        return

    percent = current / total
    arrow = '=' * int(round(percent * bar_length))
    spaces = ' ' * (bar_length - len(arrow))

    sys.stdout.write(f'\r{prefix} [{arrow}{spaces}] {int(percent * 100)}% ({current}/{total}) {suffix}')
    sys.stdout.flush()

    if current == total:
        print()

def should_ignore_path(path: Path) -> bool:
    """
    Проверяет, должен ли путь быть проигнорирован.
    Возвращает True, если путь содержит игнорируемую папку.
    """
    parts = path.parts
    for part in parts:
        if part in IGNORE_DIRS:
            return True
    return False

def find_ftl_files(root: Path, ignore_dirs: set = None) -> list[Path]:
    """
    Находит все .ftl файлы рекурсивно, игнорируя указанные папки.
    """
    if ignore_dirs is None:
        ignore_dirs = IGNORE_DIRS

    files = []
    try:
        for file in root.rglob("*.ftl"):
            # Проверяем, не находится ли файл в игнорируемой папке
            if should_ignore_path(file):
                continue
            files.append(file)
    except Exception as e:
        print(f"  [ОШИБКА ПОИСКА] {root}: {e}")
    return files

def get_all_keys_from_file(filepath: Path) -> set[str]:
    """Извлекает все ключи из .ftl файла."""
    keys = set()
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()

        for match in re.finditer(r"^([a-zA-Z_.][a-zA-Z0-9_.-]*)\s*=", content, re.MULTILINE):
            key = match.group(1)
            line_start = content.rfind('\n', 0, match.start()) + 1
            line_before = content[line_start:match.start()]
            if not line_before.strip().startswith("#") and not line_before.strip().startswith("//"):
                keys.add(key)
    except Exception as e:
        print(f"  [ОШИБКА ЧТЕНИЯ] {filepath}: {e}")
    return keys

def get_full_value(content: str, key: str) -> tuple[str, int, int]:
    """Находит ключ и извлекает полное значение."""
    key_pattern = re.compile(rf"^{re.escape(key)}\s*=", re.MULTILINE)
    match = key_pattern.search(content)
    if not match:
        return "", -1, -1

    start_pos = match.start()
    value_start = match.end()

    lines = content[value_start:].splitlines(keepends=True)
    value_lines = []
    brace_count = 0
    in_block = False
    first_line = True

    for line in lines:
        stripped = line.strip()

        if not first_line and not line.startswith((' ', '\t')):
            if re.match(r"^[a-zA-Z_.][a-zA-Z0-9_.-]*\s*=", stripped):
                break

        value_lines.append(line)

        if first_line:
            match_value = re.match(rf"^{re.escape(key)}\s*=\s*(.+)$", stripped)
            if match_value:
                value_part = match_value.group(1).strip()
                if '{$' in value_part and not value_part.endswith('}'):
                    in_block = True
                    brace_count = value_part.count('{') - value_part.count('}')
                elif value_part.endswith('}'):
                    break
            first_line = False
            continue

        if in_block:
            if stripped and not stripped.startswith("#") and not stripped.startswith("//"):
                brace_count += stripped.count('{') - stripped.count('}')
                if brace_count <= 0:
                    next_idx = len(''.join(value_lines))
                    if next_idx < len(lines):
                        next_line = lines[next_idx]
                        if not next_line.startswith((' ', '\t')):
                            break
                    else:
                        break

    value = ''.join(value_lines).strip()
    end_pos = value_start + len(''.join(value_lines))

    return value, start_pos, end_pos

def collect_all_keys(root: Path, ignore_dirs: set = None) -> tuple[dict, set]:
    """Собирает все ключи из всех .ftl файлов в директории, игнорируя указанные папки."""
    if ignore_dirs is None:
        ignore_dirs = IGNORE_DIRS

    file_keys = {}
    all_keys = set()
    files = find_ftl_files(root, ignore_dirs)
    total = len(files)

    print(f"  Обработка {total} файлов...")

    for idx, ftl_file in enumerate(files):
        keys = get_all_keys_from_file(ftl_file)
        if keys:
            file_keys[str(ftl_file)] = keys
            all_keys.update(keys)

        if (idx + 1) % 50 == 0 or idx + 1 == total:
            print_progress(idx + 1, total, prefix="  Прогресс:", suffix=f"Обработано {idx + 1}/{total} файлов")

    return file_keys, all_keys

def check_if_file_has_russian(filepath: Path) -> bool:
    """Проверяет, содержит ли файл русские буквы."""
    try:
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()

        lines = content.splitlines()
        for line in lines:
            stripped = line.strip()
            if not stripped or stripped.startswith("#") or stripped.startswith("//"):
                continue
            if re.match(r"^[a-zA-Z_.][a-zA-Z0-9_.-]*\s*=", stripped):
                match = re.match(r"^[a-zA-Z_.][a-zA-Z0-9_.-]*\s*=\s*(.+)$", stripped)
                if match:
                    value = match.group(1).strip()
                    if HAS_CYRILLIC.search(value):
                        return True
            else:
                if HAS_CYRILLIC.search(stripped):
                    return True

        return False
    except Exception:
        return False

def find_untranslated_files(root: Path, english_root: Path, ignore_dirs: set = None) -> list[Path]:
    """Находит файлы, которые не имеют перевода."""
    if ignore_dirs is None:
        ignore_dirs = IGNORE_DIRS

    untranslated = []
    files = find_ftl_files(root, ignore_dirs)
    total = len(files)

    print(f"  Проверка {total} файлов...")

    for idx, russian_file in enumerate(files):
        relative_path = russian_file.relative_to(root)
        english_file = english_root / relative_path

        if english_file.exists() and not check_if_file_has_russian(russian_file):
            untranslated.append(russian_file)

        if (idx + 1) % 20 == 0 or idx + 1 == total:
            print_progress(idx + 1, total, prefix="  Прогресс:", suffix=f"Проверено {idx + 1}/{total} файлов")

    return untranslated

def backup_russian_locale():
    """Создает резервную копию русской локализации."""
    if BACKUP_DIR.exists():
        shutil.rmtree(BACKUP_DIR)
    shutil.copytree(RUSSIAN_LOCALE, BACKUP_DIR)
    print(f"  [БЭКАП] Создана копия в {BACKUP_DIR}")

def main():
    print("=" * 70)
    print("  ОЧИСТКА РУССКОЙ ЛОКАЛИЗАЦИИ")
    print("  Сравнение с английской локализацией (эталон)")
    print("=" * 70)
    print(f"\n  Игнорируемые папки: {', '.join(IGNORE_DIRS)}")

    if not ENGLISH_LOCALE.exists():
        print(f"[ОШИБКА] Нет папки с английской локализацией: {ENGLISH_LOCALE}")
        return
    if not RUSSIAN_LOCALE.exists():
        print(f"[ОШИБКА] Нет папки с русской локализацией: {RUSSIAN_LOCALE}")
        return

    # 1. Бэкап
    print("\n[1/6] Создание резервной копии русской локализации...")
    backup_russian_locale()

    # 2. Сбор ключей из английской локализации
    print("\n[2/6] Сбор ключей из английской локализации (эталон)...")
    english_file_keys, english_all_keys = collect_all_keys(ENGLISH_LOCALE)
    print(f"  Найдено файлов: {len(english_file_keys)}")
    print(f"  Найдено ключей: {len(english_all_keys)}")

    # 3. Сбор ключей из русской локализации (игнорируя ss14-ru)
    print("\n[3/6] Сбор ключей из русской локализации (игнорируя ss14-ru)...")
    russian_file_keys, russian_all_keys = collect_all_keys(RUSSIAN_LOCALE)
    print(f"  Найдено файлов: {len(russian_file_keys)}")
    print(f"  Найдено ключей: {len(russian_all_keys)}")

    # 4. Поиск непереведенных файлов
    print("\n[4/6] Поиск непереведенных файлов...")
    untranslated_files = find_untranslated_files(RUSSIAN_LOCALE, ENGLISH_LOCALE)
    print(f"  Найдено непереведенных файлов: {len(untranslated_files)}")

    # Сохраняем лог непереведенных файлов
    with open(UNTRANSLATED_LOG, "w", encoding="utf-8") as f:
        f.write("# Файлы без перевода (не содержат русских букв в значениях)\n")
        f.write(f"# Дата: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"# Игнорируемые папки: {', '.join(IGNORE_DIRS)}\n")
        f.write("#" + "=" * 59 + "\n\n")

        if untranslated_files:
            for filepath in sorted(untranslated_files):
                rel_path = filepath.relative_to(RUSSIAN_LOCALE)
                f.write(f"{rel_path}\n")
        else:
            f.write("Все файлы имеют перевод\n")

    # 5. Поиск лишних ключей
    print("\n[5/6] Поиск лишних ключей в русской локализации...")
    extra_keys = russian_all_keys - english_all_keys
    print(f"  Лишних ключей: {len(extra_keys)}")

    # 6. Удаление лишних ключей и файлов (игнорируя ss14-ru)
    print("\n[6/6] Удаление лишних ключей и файлов...")
    print(f"  (Папка ss14-ru и её содержимое будут проигнорированы)")

    removed_keys = 0
    removed_files = 0
    processed = 0

    # Получаем список файлов для обработки (игнорируя ss14-ru)
    files_to_process = find_ftl_files(RUSSIAN_LOCALE)
    total_files = len(files_to_process)

    print(f"  Обработка {total_files} файлов...")

    for russian_file in files_to_process:
        processed += 1
        relative_path = russian_file.relative_to(RUSSIAN_LOCALE)
        english_file = ENGLISH_LOCALE / relative_path

        # Удаляем файл, если нет соответствующего английского
        if not english_file.exists():
            try:
                russian_file.unlink()
                removed_files += 1
                if removed_files % 10 == 0:
                    print(f"  [УДАЛЕН ФАЙЛ] {relative_path}")
            except Exception as e:
                print(f"  [ОШИБКА УДАЛЕНИЯ] {relative_path}: {e}")
            continue

        # Получаем ключи из обоих файлов
        try:
            russian_keys = get_all_keys_from_file(russian_file)
            english_keys = get_all_keys_from_file(english_file)
        except Exception as e:
            print(f"  [ОШИБКА ЧТЕНИЯ] {relative_path}: {e}")
            continue

        # Находим лишние ключи
        extra_in_file = russian_keys - english_keys

        if extra_in_file:
            try:
                with open(russian_file, "r", encoding="utf-8") as f:
                    content = f.read()

                for key in extra_in_file:
                    _, start_pos, end_pos = get_full_value(content, key)
                    if start_pos != -1:
                        content = content[:start_pos] + content[end_pos:]
                        removed_keys += 1

                with open(russian_file, "w", encoding="utf-8") as f:
                    f.write(content)

                if removed_keys % 50 == 0:
                    print(f"  [УДАЛЕНО КЛЮЧЕЙ] {removed_keys}")

            except Exception as e:
                print(f"  [ОШИБКА] {relative_path}: {e}")

        # Показываем прогресс
        if processed % 10 == 0 or processed == total_files:
            print_progress(processed, total_files, prefix="  Прогресс:", suffix=f"Обработано {processed}/{total_files} файлов")

    # Очищаем пустые папки (но не трогаем ss14-ru)
    print("\n  Очистка пустых папок (кроме ss14-ru)...")
    for root, dirs, files in os.walk(RUSSIAN_LOCALE, topdown=False):
        # Пропускаем папку ss14-ru
        if "ss14-ru" in Path(root).parts:
            continue

        for dir_name in dirs:
            dir_path = Path(root) / dir_name
            # Пропускаем, если папка называется ss14-ru
            if dir_name in IGNORE_DIRS:
                continue
            try:
                if not any(dir_path.iterdir()):
                    dir_path.rmdir()
                    print(f"  [УДАЛЕНА ПАПКА] {dir_path.relative_to(RUSSIAN_LOCALE)}")
            except Exception:
                pass

    # Создаем отчет
    print("\n  Создание отчета...")

    report = []
    report.append("=" * 70)
    report.append(f"  ОТЧЁТ ОЧИСТКИ РУССКОЙ ЛОКАЛИЗАЦИИ")
    report.append(f"  {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    report.append("=" * 70)
    report.append("")
    report.append(f"Источник (эталон):      {ENGLISH_LOCALE}")
    report.append(f"Очищаемая локализация:  {RUSSIAN_LOCALE}")
    report.append(f"Резервная копия:        {BACKUP_DIR}")
    report.append(f"Игнорируемые папки:     {', '.join(IGNORE_DIRS)}")
    report.append("")
    report.append(f"Ключей в английской локализации:  {len(english_all_keys)}")
    report.append(f"Ключей в русской локализации:     {len(russian_all_keys)}")
    report.append(f"Лишних ключей в русской:          {len(extra_keys)}")
    report.append("")
    report.append(f"Непереведенных файлов:   {len(untranslated_files)}")
    report.append(f"Удалено файлов:          {removed_files}")
    report.append(f"Удалено ключей:          {removed_keys}")
    report.append("")
    report.append(f"Лог непереведенных:      {UNTRANSLATED_LOG}")

    with open(REPORT_FILE, "w", encoding="utf-8") as f:
        f.write("\n".join(report))

    # Лог
    with open(LOG_FILE, "w", encoding="utf-8") as f:
        f.write(f"# Лог очистки русской локализации\n")
        f.write(f"# Дата: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")
        f.write(f"# Резервная копия: {BACKUP_DIR}\n")
        f.write(f"# Игнорируемые папки: {', '.join(IGNORE_DIRS)}\n")
        f.write("#" + "=" * 59 + "\n")
        f.write(f"Непереведенных файлов: {len(untranslated_files)}\n")
        f.write(f"Удалено файлов: {removed_files}\n")
        f.write(f"Удалено ключей: {removed_keys}\n")

    print("\n" + "=" * 70)
    print("  ГОТОВО!")
    print("=" * 70)
    print(f"  Непереведенных файлов:  {len(untranslated_files)}")
    print(f"  Удалено файлов:         {removed_files}")
    print(f"  Удалено ключей:         {removed_keys}")
    print(f"  Игнорируемые папки:     {', '.join(IGNORE_DIRS)}")
    print(f"  Резервная копия:        {BACKUP_DIR}")
    print(f"  Отчет:                  {REPORT_FILE}")
    print(f"  Лог:                    {LOG_FILE}")
    print(f"  Лог непереведенных:     {UNTRANSLATED_LOG}")
    print("=" * 70)

if __name__ == "__main__":
    main()
