#include <windows.h>
#include <cstdio>
static int callbacks=0,received=0;
static HHOOK hook=nullptr;
static constexpr ULONG_PTR tag=0x54543201;
static LRESULT CALLBACK keyboard(int code,WPARAM w,LPARAM l){
    if(code>=0 && reinterpret_cast<KBDLLHOOKSTRUCT*>(l)->dwExtraInfo==tag)++callbacks;
    return CallNextHookEx(nullptr,code,w,l);
}
static LRESULT CALLBACK window(HWND hwnd,UINT msg,WPARAM w,LPARAM l){
    if(msg==WM_KEYDOWN && w==VK_F6)++received;
    if(msg==WM_TIMER){
        if(w==1){
            KillTimer(hwnd,1);
            if(GetForegroundWindow()==hwnd){
                INPUT events[2]{};
                events[0].type=events[1].type=INPUT_KEYBOARD;
                events[0].ki.wVk=events[1].ki.wVk=VK_F6;
                events[0].ki.dwExtraInfo=events[1].ki.dwExtraInfo=tag;
                events[1].ki.dwFlags=KEYEVENTF_KEYUP;
                SendInput(2,events,sizeof(INPUT));
            }
        }else DestroyWindow(hwnd);
        return 0;
    }
    if(msg==WM_DESTROY){PostQuitMessage(0);return 0;}
    return DefWindowProcW(hwnd,msg,w,l);
}
int main(int argc,char**){
    HINSTANCE instance=GetModuleHandleW(nullptr);
    WNDCLASSW cls{};cls.lpfnWndProc=window;cls.hInstance=instance;cls.lpszClassName=L"TinyTaskNativeHookControl";
    if(!RegisterClassW(&cls))return 2;
    HWND hwnd=CreateWindowW(cls.lpszClassName,L"TinyTask native keyboard control - closes in 3 seconds",WS_OVERLAPPEDWINDOW|WS_VISIBLE,80,80,650,240,nullptr,nullptr,instance,nullptr);
    if(!hwnd)return 3;
    SetForegroundWindow(hwnd);
    RAWINPUTDEVICE devices[2]{{1,2,RIDEV_INPUTSINK,hwnd},{1,6,RIDEV_INPUTSINK,hwnd}};
    if(argc>1 && !RegisterRawInputDevices(devices,2,sizeof(RAWINPUTDEVICE)))return 5;
    hook=SetWindowsHookExW(WH_KEYBOARD_LL,keyboard,instance,0);
    if(!hook){DestroyWindow(hwnd);return 4;}
    SetTimer(hwnd,1,500,nullptr);SetTimer(hwnd,2,3000,nullptr);
    MSG message{};while(GetMessageW(&message,nullptr,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}
    UnhookWindowsHookEx(hook);
    std::printf("{\"nativeHookCallbacks\":%d,\"fixtureF6\":%d}\n",callbacks,received);
    return callbacks==2 && received==1?0:1;
}
