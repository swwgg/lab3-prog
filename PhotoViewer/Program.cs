namespace PhotoViewer;

// консольное приложение для работы с фотографиями по их путям
internal static class Program
{
    private static void Main(string[] args)
    {
        string statePath = args.Length > 0 ? args[0] : "state.json";
        var state = AppState.LoadFromFile(statePath);

        if (File.Exists(statePath))
            Console.WriteLine($"Загружено состояние из '{statePath}' (фото: {state.Photos.Count}).");
        else
            Console.WriteLine("Новое состояние приложения.");

        bool running = true;
        while (running)
        {
            PrintCurrentPhoto(state);
            PrintMenu();
            Console.Write("\nВыберите пункт: ");
            string? choice = Console.ReadLine()?.Trim();

            try
            {
                switch (choice)
                {
                    case "1":
                        Console.Write("Путь к файлу: ");
                        string path = (Console.ReadLine() ?? "").Trim().Trim('\'', '"');
                        state.Photos.Add(Photo.Open(path));
                        state.CurrentIndex = state.Photos.Count - 1;
                        Console.WriteLine($"Открыто фото '{path}'.");
                        break;
                    case "2":
                        SwitchPhoto(state);
                        break;
                    case "3":
                        PhotoEditor.Crop(RequirePhoto(state), ReadInt("X: "), ReadInt("Y: "), ReadInt("Ширина: "), ReadInt("Высота: "));
                        break;
                    case "4":
                        var photoToRotate = RequirePhoto(state);
                        Console.Write("1 - Вправо (по часовой), 2 - Влево (против часовой): ");
                        bool clockwise = Console.ReadLine()?.Trim() != "2";
                        PhotoEditor.Rotate(photoToRotate, clockwise);
                        break;
                    case "5":
                        PhotoEditor.SetPixel(RequirePhoto(state), ReadInt("X: "), ReadInt("Y: "), ReadColor("Цвет (R G B): "));
                        break;
                    case "6":
                        var photo = RequirePhoto(state);
                        Console.Write("Текст надписи: ");
                        string text = Console.ReadLine() ?? "";
                        PhotoEditor.AddText(photo, text, ReadInt("X: "), ReadInt("Y: "), ReadColor("Цвет (R G B): "));
                        break;
                    case "7":
                        ApplyFilter(RequirePhoto(state));
                        break;
                    case "8":
                        CreateCollage(state);
                        break;
                    case "9":
                        PhotoEditor.Undo(RequirePhoto(state));
                        Console.WriteLine("Последнее изменение отменено.");
                        break;
                    case "10":
                        state.CloseCurrentPhoto();
                        Console.WriteLine("Текущее фото закрыто.");
                        break;
                    case "11":
                        state.SaveToFile(statePath);
                        Console.WriteLine($"Сохранено в '{statePath}'.");
                        break;
                    case "0":
                        state.SaveToFile(statePath);
                        Console.WriteLine($"Сохранено в '{statePath}'. Выход.");
                        running = false;
                        break;
                    default:
                        Console.WriteLine("Неизвестная команда.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }

    private static void PrintCurrentPhoto(AppState state)
    {
        Console.WriteLine("\n=====================================");
        var photo = state.GetCurrentPhoto();
        if (photo == null)
        {
            Console.WriteLine("Нет открытых фотографий.");
            return;
        }

        Console.WriteLine($"Фото [{state.CurrentIndex + 1}/{state.Photos.Count}]: {photo.FilePath} ({photo.Width}x{photo.Height})");
        foreach (var item in photo.CollageItems)
            Console.WriteLine($"  [Коллаж] {item.PhotoPath} ({item.Width}x{item.Height}) в ({item.X}, {item.Y})");
        foreach (var label in photo.Labels)
            Console.WriteLine($"  [Надпись] \"{label.Text}\" в ({label.X}, {label.Y}), {label.Color}");
        foreach (var change in photo.Changes)
            Console.WriteLine($"  - {change}");
    }

    private static void PrintMenu()
    {
        Console.WriteLine("=====================================");
        Console.WriteLine("1. Открыть фото по пути   2. Переключить фото");
        Console.WriteLine("3. Обрезать фото          4. Повернуть фото на 90°");
        Console.WriteLine("5. Изменить пиксель       6. Добавить надпись");
        Console.WriteLine("7. Применить фильтр       8. Составить коллаж");
        Console.WriteLine("9. Откатить изменение     10. Закрыть текущее фото");
        Console.WriteLine("11. Сохранить состояние   0. Выход с сохранением");
    }

    private static Photo RequirePhoto(AppState state)
    {
        var photo = state.GetCurrentPhoto();
        if (photo == null)
            throw new InvalidOperationException("Сначала откройте фото.");
        return photo;
    }

    private static void SwitchPhoto(AppState state)
    {
        if (state.Photos.Count == 0)
            throw new InvalidOperationException("Список фотографий пуст.");

        for (int i = 0; i < state.Photos.Count; i++)
            Console.WriteLine($"{i + 1}. {state.Photos[i].FilePath} ({state.Photos[i].Width}x{state.Photos[i].Height})");

        int index = ReadInt("Номер фото: ") - 1;
        if (index < 0 || index >= state.Photos.Count)
            throw new ArgumentOutOfRangeException(nameof(index), "Неверный номер фото.");
        state.CurrentIndex = index;
    }

    private static void ApplyFilter(Photo photo)
    {
        Console.Write("1 - Яркость, 2 - Сдвиг RGB, 3 - Инверсия, 4 - Ч/Б: ");
        switch (Console.ReadLine()?.Trim())
        {
            case "1":
                PhotoEditor.AdjustBrightness(photo, ReadInt("Яркость (-255..255): "));
                break;
            case "2":
                PhotoEditor.AdjustColors(photo, ReadInt("dR: "), ReadInt("dG: "), ReadInt("dB: "));
                break;
            case "3":
                PhotoEditor.InvertColors(photo);
                break;
            case "4":
                PhotoEditor.ApplyGrayscale(photo);
                break;
        }
    }

    private static void CreateCollage(AppState state)
    {
        if (state.Photos.Count == 0)
            throw new InvalidOperationException("Нет фотографий для коллажа.");

        Console.Write("Путь для коллажа: ");
        string path = Console.ReadLine()?.Trim() ?? "collage.jpg";
        int w = ReadInt("Ширина холста: ");
        int h = ReadInt("Высота холста: ");
        int count = ReadInt("Сколько фото: ");

        var items = new List<(Photo Photo, int X, int Y)>();
        for (int i = 0; i < count; i++)
        {
            int idx = ReadInt($"Номер фото #{i + 1}: ") - 1;
            if (idx < 0 || idx >= state.Photos.Count)
                throw new ArgumentOutOfRangeException(nameof(idx), "Неверный номер фото.");
            items.Add((state.Photos[idx], ReadInt("X: "), ReadInt("Y: ")));
        }

        state.Photos.Add(PhotoEditor.CreateCollage(path, w, h, Pixel.Black, items));
        state.CurrentIndex = state.Photos.Count - 1;
    }

    private static int ReadInt(string prompt)
    {
        Console.Write(prompt);
        return int.Parse(Console.ReadLine() ?? "0");
    }

    private static Pixel ReadColor(string prompt)
    {
        Console.Write(prompt);
        var parts = (Console.ReadLine() ?? "0 0 0").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            throw new FormatException("Введите три числа R G B через пробел.");
        return Pixel.FromRgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }
}
