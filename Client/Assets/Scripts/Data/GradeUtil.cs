using UnityEngine;

public static class GradeUtil
{
    public static readonly Color Low  = new Color(0.7f, 0.7f, 0.7f);
    public static readonly Color Mid  = new Color(0.3f, 0.8f, 0.3f);
    public static readonly Color High = new Color(0.3f, 0.6f, 1.0f);
    public static readonly Color Top  = new Color(1.0f, 0.7f, 0.1f);

    public static Color GetColor(string grade) => grade switch
    {
        "Mid"  => Mid,
        "High" => High,
        "Top"  => Top,
        _      => Low
    };

    public static string GetHex(string grade) => grade switch
    {
        "Mid"  => "#66cc66",
        "High" => "#6699ff",
        "Top"  => "#ffcc33",
        _      => "#aaaaaa"
    };

    public static int GetSellPrice(string grade) => grade switch
    {
        "Mid"  => 100,
        "High" => 300,
        "Top"  => 800,
        _      => 30   // Low
    };
}
