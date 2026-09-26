namespace Sixthumbs.Core;

public static class InputMerger
{
    public static Xbox360State Merge(IEnumerable<Xbox360State> sources)
    {
        var merged = new Xbox360State();
        var hasLeftX = false;
        var hasLeftY = false;
        var hasRightX = false;
        var hasRightY = false;

        foreach (var source in sources)
        {
            merged.Buttons |= source.Buttons;
            if (source.LeftTrigger > merged.LeftTrigger)
            {
                merged.LeftTrigger = source.LeftTrigger;
            }

            if (source.RightTrigger > merged.RightTrigger)
            {
                merged.RightTrigger = source.RightTrigger;
            }

            merged.LeftX = MaxMagnitude(merged.LeftX, source.LeftX, ref hasLeftX);
            merged.LeftY = MaxMagnitude(merged.LeftY, source.LeftY, ref hasLeftY);
            merged.RightX = MaxMagnitude(merged.RightX, source.RightX, ref hasRightX);
            merged.RightY = MaxMagnitude(merged.RightY, source.RightY, ref hasRightY);
        }

        return merged;
    }

    private static short MaxMagnitude(short current, short candidate, ref bool initialized)
    {
        if (!initialized)
        {
            initialized = true;
            return candidate;
        }

        return Math.Abs((int)candidate) > Math.Abs((int)current) ? candidate : current;
    }
}
