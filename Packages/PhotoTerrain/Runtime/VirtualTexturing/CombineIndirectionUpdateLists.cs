using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Hollow.VirtualTexturing
{
[BurstCompile]
public struct CombineIndirectionUpdateLists : IJob
{
    public NativeArray<UnsafeUniqueIndirectionUpdateList> Lists;
    public UniqueIndirectionUpdateList                    Output;

    public void Execute()
    {
        // Output.Clear(); // We might not have any feedback frames so clear output only when new feedback comes in
        for (int iList = 0; iList < Lists.Length; iList++)
        {
            Output.Merge(Lists[iList]);
        }
    }
}
}