// Tests the actual WriteReport/GetFeatureReport source with WDF request doubles.
// No driver loading, device access, input injection, or elevation occurs here.
#include <Windows.h>
#include <cstdio>
#include <cstring>
#include <algorithm>
#include "fakerinputcommon.h"
using NTSTATUS = LONG;
#define NT_SUCCESS(s) ((s) >= 0)
#define STATUS_SUCCESS ((NTSTATUS)0)
#ifndef STATUS_INVALID_PARAMETER
#define STATUS_INVALID_PARAMETER ((NTSTATUS)0xC000000DL)
#endif
#define STATUS_INVALID_BUFFER_SIZE ((NTSTATUS)0xC0000206L)
#define STATUS_BUFFER_TOO_SMALL ((NTSTATUS)0xC0000023L)
#define STATUS_UNSUCCESSFUL ((NTSTATUS)0xC0000001L)
#define FAKERINPUT_MIN_API_VERSION 1
#define KdPrint(x) ((void)0)
struct HID_XFER_PACKET { BYTE* reportBuffer; ULONG reportBufferLen; BYTE reportId; };
struct Request { HID_XFER_PACKET packet{}; BYTE output[80]{}; size_t capacity=80, information=0; NTSTATUS bufferStatus=0, result=0; int completed=0; };
using WDFREQUEST = Request*;
struct Context { WDFREQUEST ManualQueue; };
using PDEVICE_CONTEXT = Context*;
static bool invalidCopy;
static int dequeues;
static NTSTATUS RequestGetHidXferPacket_ToWriteToDevice(WDFREQUEST request,HID_XFER_PACKET* packet){*packet=request->packet;return 0;}
static NTSTATUS RequestGetHidXferPacket_ToReadFromDevice(WDFREQUEST request,HID_XFER_PACKET* packet){*packet=request->packet;return 0;}
static NTSTATUS WdfIoQueueRetrieveNextRequest(WDFREQUEST queue,WDFREQUEST* request){dequeues++;if(!queue)return STATUS_UNSUCCESSFUL;*request=queue;return 0;}
static NTSTATUS WdfRequestRetrieveOutputBuffer(WDFREQUEST request,size_t minimum,void** buffer,size_t* capacity){if(request->bufferStatus<0)return request->bufferStatus;if(minimum>request->capacity)return STATUS_BUFFER_TOO_SMALL;*buffer=request->output;*capacity=request->capacity;return 0;}
static void WdfRequestCompleteWithInformation(WDFREQUEST request,NTSTATUS status,size_t information){request->completed++;request->result=status;request->information=information;}
static void WdfRequestSetInformation(WDFREQUEST request,size_t information){request->information=information;}
static void checkedCopy(void* destination,const void* source,size_t length){if(!destination || !source || length>80){invalidCopy=true;return;}std::memcpy(destination,source,length);}
#undef RtlCopyMemory
#define RtlCopyMemory checkedCopy
#include "routines.inc"

int main(){
    int total=0,failed=0;
    auto check=[&](const char* name,bool ok){total++;if(!ok){failed++;std::printf("FAIL %s\n",name);}};
    BYTE bytes[80]{};Request writer{},reader{};Context context{&reader};
    auto reset=[&](){std::memset(bytes,0,sizeof(bytes));writer=Request{};reader=Request{};context.ManualQueue=&reader;dequeues=0;invalidCopy=false;writer.packet={bytes,65,REPORTID_CONTROL};bytes[0]=REPORTID_CONTROL;bytes[1]=9;bytes[2]=REPORTID_KEYBOARD;};
#ifdef TT_BASELINE
    reset();reader.bufferStatus=STATUS_UNSUCCESSFUL;WriteReport(&context,&writer);check("baseline attempts copy after failed retrieval",invalidCopy);
    reset();WriteReport(&context,&writer);check("baseline returns buffer capacity instead of report bytes",reader.information!=9);
    reset();writer.packet.reportId=REPORTID_API_VERSION_FEATURE_ID;writer.packet.reportBufferLen=1;bytes[0]=0xCD;auto featureResult=GetFeatureReport(&context,&writer);check("baseline writes beyond declared feature buffer",featureResult==0 && bytes[0]!=0xCD);
#else
    for(ULONG size=0;size<3;size++){reset();writer.packet.reportBufferLen=size;auto result=WriteReport(&context,&writer);check("short control rejected before dequeue",result<0 && dequeues==0 && !invalidCopy);}
    reset();writer.packet.reportBuffer=nullptr;check("null packet rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();writer.packet.reportBufferLen=66;check("oversized control rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();bytes[1]=64;check("declared length beyond packet rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();bytes[1]=0;check("zero length rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();bytes[0]=3;check("outer report ID mismatch rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();bytes[2]=99;check("unknown inner report rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();bytes[1]=8;check("truncated keyboard rejected",WriteReport(&context,&writer)<0 && dequeues==0);
    reset();reader.bufferStatus=STATUS_UNSUCCESSFUL;auto result=WriteReport(&context,&writer);check("failed output retrieval never copies",result<0 && !invalidCopy && reader.completed==1 && reader.information==0 && writer.information==0);
    reset();reader.capacity=3;result=WriteReport(&context,&writer);check("short reader completed once with no copy",result<0 && !invalidCopy && reader.completed==1 && reader.information==0);
    reset();context.ManualQueue=nullptr;check("empty queue fails without completion",WriteReport(&context,&writer)<0 && reader.completed==0 && !invalidCopy);
    for(auto pair: {std::pair<BYTE,BYTE>{BYTE(1),BYTE(9)},{BYTE(2),BYTE(4)},{BYTE(3),BYTE(8)},{BYTE(4),BYTE(7)}}){reset();bytes[1]=pair.second;bytes[2]=pair.first;bytes[3]=5;std::fill(reader.output,reader.output+80,BYTE(0xCD));result=WriteReport(&context,&writer);check("valid report copies exact wire bytes",result==0 && !invalidCopy && reader.completed==1 && reader.information==pair.second && writer.information==65 && reader.output[0]==pair.first && reader.output[1]==5 && reader.output[pair.second]==0xCD);}
    reset();bytes[1]=8;bytes[2]=4;result=WriteReport(&context,&writer);check("absolute mouse ABI padding not delivered",result==0 && reader.information==7);
    reset();writer.packet.reportId=REPORTID_CHECK_API_VERSION;bytes[0]=REPORTID_CHECK_API_VERSION;writer.packet.reportBufferLen=1;check("short version input rejected",WriteReport(&context,&writer)<0);
    reset();writer.packet.reportId=REPORTID_CHECK_API_VERSION;FakerInputAPIVersionReport version{};version.ReportID=REPORTID_CHECK_API_VERSION;version.ApiVersion=1;std::memcpy(bytes,&version,sizeof(version));check("version input accepted",WriteReport(&context,&writer)==0 && writer.information==65);
    reset();writer.packet.reportId=REPORTID_API_VERSION_FEATURE_ID;writer.packet.reportBufferLen=1;bytes[0]=0xCD;check("short feature output unchanged",GetFeatureReport(&context,&writer)<0 && bytes[0]==0xCD && writer.information==0);
    reset();writer.packet.reportId=REPORTID_API_VERSION_FEATURE_ID;std::fill(bytes,bytes+80,BYTE(0xCD));result=GetFeatureReport(&context,&writer);FakerInputAPIVersionFeature feature{};std::memcpy(&feature,bytes,sizeof(feature));check("feature response initializes padding and bounds",result==0 && feature.ReportId==REPORTID_API_VERSION_FEATURE_ID && feature.ApiVersion==1 && bytes[1]==0 && bytes[2]==0 && bytes[3]==0 && writer.information==sizeof(feature) && bytes[sizeof(feature)]==0xCD);
#endif
#ifdef TT_BASELINE
    const char* scope = "Original routines: successful reproduction of defects with WDF doubles; no driver loaded";
#else
    const char* scope = "Patched routines with WDF doubles; no device or driver loaded";
#endif
    std::printf("{\"passed\":%s,\"checks\":%d,\"failed\":%d,\"scope\":\"%s\"}\n",failed?"false":"true",total,failed,scope);
    return failed?1:0;
}
