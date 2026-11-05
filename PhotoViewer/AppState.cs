using System.Text.Json;

namespace PhotoViewer;

// состояние приложения: список открытых фотографий и индекс активной фотографии
public class AppState
{
    public List<Photo> Photos { get; set; } = new List<Photo>();
    public int CurrentIndex { get; set; }

    public Photo? GetCurrentPhoto()
    {
        if (Photos.Count == 0)
            return null;

        CurrentIndex = Math.Clamp(CurrentIndex, 0, Photos.Count - 1);
        return Photos[CurrentIndex];
    }

    public void CloseCurrentPhoto()
    {
        if (Photos.Count == 0)
            throw new InvalidOperationException("Нет открытых фотографий для закрытия.");

        CurrentIndex = Math.Clamp(CurrentIndex, 0, Photos.Count - 1);
        Photos.RemoveAt(CurrentIndex);

        if (Photos.Count == 0)
            CurrentIndex = 0;
        else if (CurrentIndex >= Photos.Count)
            CurrentIndex = Photos.Count - 1;
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, Photo.JsonOptions);
    }

    public static AppState FromJson(string json)
    {
        return JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
    }

    public void SaveToFile(string filePath)
    {
        File.WriteAllText(filePath, ToJson());
    }

    public static AppState LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            return new AppState();

        string json = File.ReadAllText(filePath);
        return FromJson(json);
    }
}
