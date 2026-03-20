// Helper: HSL to RGB conversion without System.Drawing.
// Returns #RRGGBB hex.

public static string HslToHex(double h, double s, double l)
{
    // h: degrees 0..360, s/l: 0..1
    h = (h % 360 + 360) % 360;
    s = Math.Clamp(s, 0, 1);
    l = Math.Clamp(l, 0, 1);

    var c = (1 - Math.Abs(2 * l - 1)) * s;
    var x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
    var m = l - c / 2;

    double r1, g1, b1;
    if (h < 60)      { r1 = c; g1 = x; b1 = 0; }
    else if (h < 120){ r1 = x; g1 = c; b1 = 0; }
    else if (h < 180){ r1 = 0; g1 = c; b1 = x; }
    else if (h < 240){ r1 = 0; g1 = x; b1 = c; }
    else if (h < 300){ r1 = x; g1 = 0; b1 = c; }
    else             { r1 = c; g1 = 0; b1 = x; }

    var r = (int)Math.Round((r1 + m) * 255);
    var g = (int)Math.Round((g1 + m) * 255);
    var b = (int)Math.Round((b1 + m) * 255);

    r = Math.Clamp(r, 0, 255);
    g = Math.Clamp(g, 0, 255);
    b = Math.Clamp(b, 0, 255);

    return $"#{r:X2}{g:X2}{b:X2}";
}
