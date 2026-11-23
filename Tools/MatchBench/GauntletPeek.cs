using System;
using SquashSim;
using SquashSim.Modes;
static class GauntletPeek
{
    public static void Run()
    {
        var run = new GauntletRun();
        for (int f = 1; f <= 20; f++)
        {
            var fl = Gauntlet.Floor(run, f);
            Console.WriteLine(f + " kind " + fl.Kind + " style '" + fl.Style + "' name '" + fl.Name + "' title '" + fl.Title + "' styleName '" + SquashSim.Career.Roster.StyleName(fl.Style) + "' twists " + (fl.Twists == null ? "NULL" : fl.Twists.Count.ToString()) + " traits " + (fl.Traits == null ? "NULL" : fl.Traits.Count.ToString()) + " sig " + (fl.Signature == null ? "NULL" : fl.Signature.Count.ToString()));
        }
    }
}
