namespace DesktopBaskets;

internal static class Magnet
{
    public const int Threshold=16;
    static int Closest(IEnumerable<int> deltas)=>deltas.Where(d=>Math.Abs(d)<=Threshold).OrderBy(Math.Abs).Select(d=>(int?)d).FirstOrDefault()??0;
    public static Rectangle Move(Rectangle rect,IEnumerable<Rectangle> neighbors)
    {
        var others=neighbors.ToArray();var horizontal=new List<int>();
        foreach(var other in others)
        {
            horizontal.Add(other.Left-rect.Left);horizontal.Add(other.Right-rect.Right);
            if(rect.Top<other.Bottom+Threshold&&rect.Bottom>other.Top-Threshold)
            {horizontal.Add(other.Left-rect.Right);horizontal.Add(other.Right-rect.Left);}
        }
        rect.Offset(Closest(horizontal),0);var vertical=new List<int>();
        foreach(var other in others)
        {
            vertical.Add(other.Top-rect.Top);vertical.Add(other.Bottom-rect.Bottom);
            if(rect.Left<other.Right+Threshold&&rect.Right>other.Left-Threshold)
            {vertical.Add(other.Top-rect.Bottom);vertical.Add(other.Bottom-rect.Top);}
        }
        rect.Offset(0,Closest(vertical));return rect;
    }
}
