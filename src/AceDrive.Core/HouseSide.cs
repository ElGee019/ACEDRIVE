// Which side of the street a house number sits on. Unity's HouseNumberGenerator
// gives odd numbers one side and even the other so a street is numbered the way
// real streets are. The HUD does not print the side; it grounds the marker
// network (PORTPLAN 24/59) that will place each address in the world.
namespace AceDrive.Core;

/// <summary>Side of the street a house number belongs to.</summary>
public enum HouseSide
{
    Left,
    Right,
}
