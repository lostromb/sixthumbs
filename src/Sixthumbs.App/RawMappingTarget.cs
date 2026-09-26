using Sixthumbs.Core;

namespace Sixthumbs.App;

public sealed class RawMappingTarget : IEquatable<RawMappingTarget>
{
    private RawMappingTarget(Xbox360Control? control)
    {
        Control = control;
        Label = control?.DisplayName() ?? "None";
    }

    public Xbox360Control? Control { get; }
    public string Label { get; }

    public static RawMappingTarget None { get; } = new(null);

    public static IReadOnlyList<RawMappingTarget> All { get; } =
        new[] { None }.Concat(Xbox360Controls.All.Select(control => new RawMappingTarget(control))).ToArray();

    public static RawMappingTarget From(Xbox360Control? control) =>
        control is null ? None : All.First(item => item.Control == control);

    public bool Equals(RawMappingTarget? other) => other is not null && Control == other.Control;

    public override bool Equals(object? obj) => obj is RawMappingTarget other && Equals(other);

    public override int GetHashCode() => Control?.GetHashCode() ?? 0;

    public override string ToString() => Label;
}
