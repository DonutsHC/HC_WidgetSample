using System;
using System.IO;
using UnityEngine;

public class WidgetDataWriter : MonoBehaviour
{
    /// <summary>
    /// Widget data file name. Must match WIDGET_DATA_FILE in GameWidgetProvider.java.
    /// </summary>
    const string FILE_NAME = "widget_data.json";

    /// <summary>
    /// Day boundary hour. Before this hour counts as the previous day.
    /// Must match DAY_BOUNDARY_HOUR in GameWidgetProvider.java.
    /// </summary>
    const int DAY_BOUNDARY_HOUR = 6;

    void Start()
    {
        RecordLogin();
    }

    /// <summary>
    /// Returns the "effective date" for login purposes.
    /// Day boundary is 6:00 AM — playing before 6am counts as the previous day.
    /// </summary>
    DateTime GetEffectiveDate()
    {
        return DateTime.Now.AddHours(-DAY_BOUNDARY_HOUR).Date;
    }

    public void RecordLogin()
    {
        var data = new WidgetData
        {
            lastLoginDate = GetEffectiveDate().ToString("yyyy-MM-dd"),
            streak = LoadCurrentStreak()
        };

        var json = JsonUtility.ToJson(data, prettyPrint: true);
        var path = Path.Combine(Application.persistentDataPath, FILE_NAME);
        File.WriteAllText(path, json);

        Debug.Log($"[WidgetDataWriter] Saved widget data to {path}");
        WidgetUtility.RequestWidgetUpdate();
    }

    int LoadCurrentStreak()
    {
        var path = Path.Combine(Application.persistentDataPath, FILE_NAME);

        if (!File.Exists(path))
            return 1;

        try
        {
            var existingJson = File.ReadAllText(path);
            var existing = JsonUtility.FromJson<WidgetData>(existingJson);
            DateTime lastLogin = DateTime.Parse(existing.lastLoginDate);
            DateTime effectiveToday = GetEffectiveDate();

            if (lastLogin == effectiveToday)
            {
                return existing.streak;
            }
            else if (lastLogin == effectiveToday.AddDays(-1))
            {
                return existing.streak + 1;
            }
            else
            {
                return 1;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WidgetDataWriter] Could not read existing data: {e.Message}");
            return 1;
        }
    }
}
