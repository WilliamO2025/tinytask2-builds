#include "filter_policy.h"
#include <cstdio>
#include <cstdlib>
static int checks=0;
static void check(bool ok){++checks;if(!ok){std::fprintf(stderr,"Failed check %d\n",checks);std::exit(1);}}
int main(){
    using namespace tinytask;
    FilterPolicy policy;MousePacket packet{7,-9,1,120,2,42,0x1234,0xAABBCCDD},result{};
    check(policy.Forward(1,packet,0));
    check(!policy.Arm(0,5,0,true,true));
    check(!policy.Arm(1,0,0,true,true));
    check(!policy.Arm(1,5,0,false,true));
    check(!policy.Arm(1,5,0,true,false));
    check(policy.Arm(1,5,10,true,true));
    check(!policy.Arm(2,6,11,true,true));
    check(policy.Forward(2,packet,11));
    check(!policy.Forward(1,packet,11));
    check(!policy.Take(6,result,12));
    check(policy.Take(5,result,12) && result.dx==7 && result.dy==-9 && result.buttonFlags==1 && result.buttonData==120 && result.flags==2 && result.unitId==42 && result.rawButtons==0x1234 && result.extraInformation==0xAABBCCDD);
    check(!policy.Take(5,result,12));
    check(!policy.Renew(6,20));check(policy.Renew(5,400));
    check(policy.Active(899));check(!policy.Active(900));
    check(!policy.Renew(5,901));check(policy.Forward(1,packet,901));
    check(policy.Arm(1,5,1000,true,true));
    for(int i=0;i<64;++i)check(!policy.Forward(1,packet,1001));
    check(policy.Forward(1,packet,1002));check(!policy.Active(1002));
    check(!policy.Take(5,result,1002));
    check(policy.Arm(1,5,1100,true,true));policy.Disconnect(2);check(policy.Active(1100));
    policy.Disconnect(1);check(!policy.Active(1100));
    check(policy.Arm(1,5,1200,true,true));policy.Close(6);check(policy.Active(1200));
    policy.Close(5);check(!policy.Active(1200));
    check(policy.Arm(1,5,1300,true,true));check(!policy.Active(1299));
    check(policy.Arm(1,5,1400,true,true));check(!policy.Forward(1,packet,1401));
    policy.Stop();check(!policy.Take(5,result,1402));check(policy.Forward(1,packet,1402));
    std::printf("Passed %d filter policy checks. No Windows input filtering was installed or tested.\n",checks);
}
