namespace XIVBlackjack.Data;

public class Roll
{
    public int Result = 1000;
    public int OutOf = 1000;
    public string PlayerName;

    private Roll(string name)
    {
        PlayerName = name;
    }

    public Roll(string fullName, int roll, int outOf)
    {
        Result = roll;
        OutOf = outOf != 0 ? outOf : -1;
        PlayerName = fullName;
    }

    public static Roll Dummy(string name = "Unknown") => new(name);
}
