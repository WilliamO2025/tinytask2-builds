using System;
using TinyTask;
var original = new double[] { 0.5, 0.25, 0.75 };
var timeline = new PlaybackTimeline(original, 2);
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
Check(timeline.Due(0,0)==0.25, "recorded initial delay; no extra countdown");
Check(timeline.Due(0,2)==0.75, "speed applied once to original timeline");
Check(timeline.Due(1000000,2)==750000.75, "loop deadlines anchored without iterative accumulation");
Check(timeline.Due(0,1)==0.375, "late execution cannot shift next deadline");
Check(original[0]==0.5 && original[1]==0.25, "source delays unchanged");
original[0]=100;
Check(timeline.Due(0,0)==0.25, "compiled snapshot unaffected by source edits");
Check(timeline.Due(2,0,3)==4.75, "explicit scheduled start preserved");
foreach (double invalid in new[]{double.NaN,double.PositiveInfinity,0,-1,1001}) {
 bool rejected=false; try { _=new PlaybackTimeline(original,invalid); } catch(ArgumentException){rejected=true;}
 Check(rejected,"invalid speed rejected: " + invalid);
}
Check(new PlaybackTimeline(new double[]{0,0},1).Due(50,1)==0,"zero delay timeline");
