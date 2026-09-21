#include <windows.h>
#include <shellapi.h>
#include <atomic>
#include <cstdio>
#include <string>
#include <algorithm>

// Bounded, owned-window experiment. Never targets Roblox or another application.
static HWND target=nullptr,work=nullptr,previous=nullptr;
static HWND controller=nullptr;
static HANDLE observer=nullptr;
static DWORD observerThread=0;
static bool rawReady=false;
static DWORD controllerPid=0;
static std::atomic<HHOOK> mouseHook{},keyHook{};
static std::atomic<bool> running=false;
static std::atomic<int> unhookFailures=0;
static HANDLE watchdog=nullptr;
static ULONGLONG began=0;
static POINT saved{},virtualPoint{180,160};
static int macroKeys=0,macroClicks=0,legacyA=0,rawA=0,rawMouse=0,workKeys=0,workClicks=0,hookKeys=0;
static int baseA=0,baseRawA=0,baseRawMouse=0,baseClicks=0,beats=0;
static bool automatic=false,sentControl=false,startedRouting=false;
static std::wstring report;
static std::wstring status=L"Click Start. Use only A and the left mouse button. F10 stops.";
static constexpr ULONG_PTR macroTag=0x54543201,controlTag=0x54543202;
static bool active(){return running.load() && GetTickCount64()-began<20000 && GetForegroundWindow()==target;}
static void release(){
    running=false;
    if(auto h=mouseHook.exchange(nullptr))if(!UnhookWindowsHookEx(h))++unhookFailures;
    if(auto h=keyHook.exchange(nullptr))if(!UnhookWindowsHookEx(h))++unhookFailures;
}
static void CALLBACK expire(PVOID,BOOLEAN){release();}
static void stop(const wchar_t* reason){
    if(!began)return;
    release();KillTimer(target,1);
    if(watchdog){DeleteTimerQueueTimer(nullptr,watchdog,INVALID_HANDLE_VALUE);watchdog=nullptr;}
    status=reason;
    bool passed=sentControl && workKeys>=1 && workClicks>=1 && legacyA==baseA && macroKeys>0 && macroClicks>baseClicks;
    FILE* file=nullptr;
    if(_wfopen_s(&file,report.c_str(),L"w")==0){
        std::fprintf(file,"{\"automatic\":%s,\"syntheticPassed\":%s,\"physicalIsolationProven\":false,"
          "\"macroKeys\":%d,\"macroClicks\":%d,\"workKeys\":%d,\"workClicks\":%d,\"keyboardHookCallbacks\":%d,"
          "\"baseline\":{\"legacyA\":%d,\"rawA\":%d,\"rawMouse\":%d},"
          "\"routing\":{\"legacyA\":%d,\"rawA\":%d,\"rawMouse\":%d},\"unhookApiFailures\":%d}\n",
          automatic?"true":"false",automatic?(passed?"true":"false"):"null",macroKeys,macroClicks-baseClicks,workKeys,workClicks,hookKeys,
          baseA,baseRawA,baseRawMouse,legacyA-baseA,rawA-baseRawA,rawMouse-baseRawMouse,unhookFailures.load());
        std::fclose(file);
    }else status+=L" Report could not be written.";
    began=0;
    if(GetForegroundWindow()==target){SetCursorPos(saved.x,saved.y);if(IsWindow(previous)&&previous!=target)SetForegroundWindow(previous);}
    EnableWindow(GetDlgItem(target,1),TRUE);InvalidateRect(target,nullptr,TRUE);
    if(automatic)PostMessageW(target,WM_CLOSE,0,0);
}
static LRESULT CALLBACK keyboard(int code,WPARAM message,LPARAM data){
    if(code>=0 && active()){
        ++hookKeys;
        const auto& k=*reinterpret_cast<KBDLLHOOKSTRUCT*>(data);
        if(k.dwExtraInfo==macroTag || ((k.flags&LLKHF_INJECTED) && k.dwExtraInfo!=controlTag))return CallNextHookEx(nullptr,code,message,data);
        if(k.vkCode=='A'){
            LPARAM flags=1 | (static_cast<LPARAM>(k.scanCode)<<16);
            if(message==WM_KEYUP)flags|=static_cast<LPARAM>(0xC0000000u);
            if(PostMessageW(work,static_cast<UINT>(message),'A',flags))return 1;
        }
        release();
    }
    return CallNextHookEx(nullptr,code,message,data);
}
static LRESULT CALLBACK mouse(int code,WPARAM message,LPARAM data){
    if(code>=0 && active()){
        const auto& m=*reinterpret_cast<MSLLHOOKSTRUCT*>(data);
        if(m.dwExtraInfo==macroTag || ((m.flags&LLMHF_INJECTED) && m.dwExtraInfo!=controlTag))return CallNextHookEx(nullptr,code,message,data);
        if(message==WM_MOUSEMOVE || message==WM_LBUTTONDOWN || message==WM_LBUTTONUP){
            POINT real{};GetCursorPos(&real);
            virtualPoint.x=std::clamp(virtualPoint.x+m.pt.x-real.x,5L,395L);
            virtualPoint.y=std::clamp(virtualPoint.y+m.pt.y-real.y,5L,275L);
            if(PostMessageW(work,static_cast<UINT>(message),message==WM_LBUTTONDOWN?MK_LBUTTON:0,MAKELPARAM(virtualPoint.x,virtualPoint.y)))return 1;
        }
        release();
    }
    return CallNextHookEx(nullptr,code,message,data);
}
static void injectKey(WORD key,ULONG_PTR tag){
    if(!active())return;
    INPUT e[2]{};e[0].type=e[1].type=INPUT_KEYBOARD;e[0].ki.wVk=e[1].ki.wVk=key;
    e[0].ki.dwExtraInfo=e[1].ki.dwExtraInfo=tag;e[1].ki.dwFlags=KEYEVENTF_KEYUP;
    if(SendInput(2,e,sizeof(INPUT))!=2)release();
}
static void injectClick(ULONG_PTR tag){
    POINT p{};GetCursorPos(&p);if(!active() || WindowFromPoint(p)!=target)return;
    INPUT e[2]{};e[0].type=e[1].type=INPUT_MOUSE;e[0].mi.dwFlags=MOUSEEVENTF_LEFTDOWN;e[1].mi.dwFlags=MOUSEEVENTF_LEFTUP;
    e[0].mi.dwExtraInfo=e[1].mi.dwExtraInfo=tag;if(SendInput(2,e,sizeof(INPUT))!=2)release();
}
static void begin(){
    if(began)return;
    if(!rawReady || WaitForSingleObject(observer,0)!=WAIT_TIMEOUT){status=L"Raw input observer unavailable. Close and reopen this experiment.";InvalidateRect(target,nullptr,TRUE);return;}
    for(int key:{VK_LBUTTON,VK_RBUTTON,VK_MBUTTON,VK_SHIFT,VK_CONTROL,VK_MENU,VK_LWIN,VK_RWIN,0x41})
        if(GetAsyncKeyState(key)<0){status=L"Release keys/buttons and try again.";InvalidateRect(target,nullptr,TRUE);return;}
    previous=GetForegroundWindow();GetCursorPos(&saved);SetForegroundWindow(target);SetFocus(target);
    if(GetForegroundWindow()!=target){status=L"Test window could not get focus. No hooks installed.";return;}
    POINT p{300,190};ClientToScreen(target,&p);SetCursorPos(p.x,p.y);
    macroKeys=macroClicks=legacyA=rawA=rawMouse=workKeys=workClicks=hookKeys=0;unhookFailures=0;
    baseA=baseRawA=baseRawMouse=baseClicks=beats=0;sentControl=startedRouting=false;
    began=GetTickCount64();running=true;
    if(!CreateTimerQueueTimer(&watchdog,nullptr,expire,nullptr,22000,0,WT_EXECUTEONLYONCE)){stop(L"Safety timer setup failed");return;}
    if(!SetTimer(target,1,100,nullptr)){stop(L"UI timer setup failed");return;}
    EnableWindow(GetDlgItem(target,1),FALSE);
}
static void tick(){
    if(!active() || WaitForSingleObject(observer,0)!=WAIT_TIMEOUT){stop(L"Stopped: timeout, focus change, other key/button, or input error.");return;}
    if(GetTickCount64()-began>=5000 && !startedRouting){
        baseA=legacyA;baseRawA=rawA;baseRawMouse=rawMouse;baseClicks=macroClicks;startedRouting=true;
        mouseHook=SetWindowsHookExW(WH_MOUSE_LL,mouse,GetModuleHandleW(nullptr),0);
        keyHook=SetWindowsHookExW(WH_KEYBOARD_LL,keyboard,GetModuleHandleW(nullptr),0);
        if(!mouseHook || !keyHook){stop(L"Hook setup failed");return;}
    }
    if(startedRouting && ++beats%7==0){injectKey(VK_F6,macroTag);injectClick(macroTag);}
    if(startedRouting && automatic && !sentControl){sentControl=true;injectKey('A',controlTag);injectClick(controlTag);}
    InvalidateRect(target,nullptr,TRUE);InvalidateRect(work,nullptr,TRUE);
}
static LRESULT CALLBACK window(HWND hwnd,UINT msg,WPARAM w,LPARAM l){
    bool isWork=hwnd==work;
    if(msg==WM_COMMAND && !isWork && LOWORD(w)==1){begin();return 0;}
    if(msg==WM_TIMER && !isWork){tick();return 0;}
    if(msg==WM_APP+1){rawReady=true;if(automatic)PostMessageW(target,WM_COMMAND,1,0);return 0;}
    if(msg==WM_APP+2){if(running){if(w==0)++rawMouse;else if(w==1)++rawA;}return 0;}
    if(msg==WM_KEYDOWN){if(w=='A'){if(isWork)++workKeys;else if(running)++legacyA;}if(!isWork && running && w==VK_F6)++macroKeys;return 0;}
    if(msg==WM_KEYUP)return 0;
    if(msg==WM_LBUTTONUP){if(isWork)++workClicks;else if(running)++macroClicks;return 0;}
    if(isWork && (msg==WM_MOUSEMOVE || msg==WM_LBUTTONDOWN))return 0;
    if(msg==WM_PAINT){
        PAINTSTRUCT ps{};HDC dc=BeginPaint(hwnd,&ps);RECT rect{};GetClientRect(hwnd,&rect);FillRect(dc,&rect,GetSysColorBrush(COLOR_WINDOW));
        wchar_t text[1400]{};
        if(isWork){swprintf_s(text,L"Background work fixture\nA presses: %d\nClicks: %d\n\nBlue dot = routed user pointer",workKeys,workClicks);}
        else swprintf_s(text,L"%s\n\nOnly use A, mouse movement and left clicks.\nF10 or any other key/button stops the experiment.\nAutomatic stop after 20 seconds.\n\nMacro F6: %d    Target A: %d    Target clicks: %d\nPhysical raw packets: A=%d, mouse=%d\n%s",
            running?(startedRouting?L"ROUTING PHASE":L"BASELINE PHASE - move mouse and tap A"):status.c_str(),macroKeys,legacyA,macroClicks,rawA,rawMouse,report.c_str());
        rect.left+=20;rect.top+=15;rect.right-=15;DrawTextW(dc,text,-1,&rect,DT_LEFT|DT_WORDBREAK);
        if(isWork){HBRUSH brush=CreateSolidBrush(RGB(55,100,220));auto old=SelectObject(dc,brush);Ellipse(dc,virtualPoint.x-6,virtualPoint.y-6,virtualPoint.x+6,virtualPoint.y+6);SelectObject(dc,old);DeleteObject(brush);}
        EndPaint(hwnd,&ps);return 0;
    }
    if(msg==WM_CLOSE){
        if(isWork && IsWindow(target)){PostMessageW(target,WM_CLOSE,0,0);return 0;}
        stop(L"Window closed");DestroyWindow(hwnd);return 0;
    }
    if(msg==WM_DESTROY){if(!isWork){release();if(IsWindow(work))DestroyWindow(work);PostQuitMessage(0);}return 0;}
    return DefWindowProcW(hwnd,msg,w,l);
}
static LRESULT CALLBACK observe(HWND hwnd,UINT msg,WPARAM w,LPARAM l){
    if(msg==WM_INPUT){
        RAWINPUT input{};UINT bytes=sizeof(input);
        if(GetRawInputData(reinterpret_cast<HRAWINPUT>(l),RID_INPUT,&input,&bytes,sizeof(RAWINPUTHEADER))!=UINT(-1) && input.header.hDevice){
            if(input.header.dwType==RIM_TYPEMOUSE)PostMessageW(controller,WM_APP+2,0,0);
            if(input.header.dwType==RIM_TYPEKEYBOARD && input.data.keyboard.VKey=='A' && !(input.data.keyboard.Flags&RI_KEY_BREAK))PostMessageW(controller,WM_APP+2,1,0);
        }
    }
    if(msg==WM_TIMER){DWORD pid=0;GetWindowThreadProcessId(controller,&pid);if(!IsWindow(controller) || pid!=controllerPid)PostQuitMessage(0);return 0;}
    return DefWindowProcW(hwnd,msg,w,l);
}
int WINAPI wWinMain(HINSTANCE instance,HINSTANCE,PWSTR,int){
    SetProcessDPIAware();int argc=0;auto argv=CommandLineToArgvW(GetCommandLineW(),&argc);
    if(argc==3 && std::wstring(argv[1])==L"--raw-observer"){
        controller=reinterpret_cast<HWND>(_wcstoui64(argv[2],nullptr,10));LocalFree(argv);
        wchar_t name[80]{};GetClassNameW(controller,name,80);if(std::wstring(name)!=L"TinyTaskRouteNative")return 5;
        GetWindowThreadProcessId(controller,&controllerPid);
        WNDCLASSW cls{};cls.lpfnWndProc=observe;cls.hInstance=instance;cls.lpszClassName=L"TinyTaskRawObserver";
        if(!RegisterClassW(&cls))return 6;
        HWND hwnd=CreateWindowW(cls.lpszClassName,L"",0,0,0,0,0,HWND_MESSAGE,nullptr,instance,nullptr);
        RAWINPUTDEVICE devices[2]{{1,2,RIDEV_INPUTSINK,hwnd},{1,6,RIDEV_INPUTSINK,hwnd}};
        if(!hwnd || !RegisterRawInputDevices(devices,2,sizeof(RAWINPUTDEVICE)))return 7;
        began=GetTickCount64();SetTimer(hwnd,1,500,nullptr);PostMessageW(controller,WM_APP+1,0,0);
        MSG m{};while(GetMessageW(&m,nullptr,0,0)>0){TranslateMessage(&m);DispatchMessageW(&m);}DestroyWindow(hwnd);return 0;
    }
    wchar_t local[MAX_PATH]{};GetEnvironmentVariableW(L"LOCALAPPDATA",local,MAX_PATH);
    std::wstring dir=std::wstring(local)+L"\\TinyTask2-RouteProbe";CreateDirectoryW(dir.c_str(),nullptr);report=dir+L"\\native-result.json";
    for(int i=1;i<argc;i++){if(std::wstring(argv[i])==L"--self-test")automatic=true;if(std::wstring(argv[i])==L"--report" && i+1<argc)report=argv[++i];}LocalFree(argv);
    WNDCLASSW cls{};cls.lpfnWndProc=window;cls.hInstance=instance;cls.lpszClassName=L"TinyTaskRouteNative";cls.hCursor=LoadCursorW(nullptr,IDC_ARROW);cls.hbrBackground=GetSysColorBrush(COLOR_WINDOW);
    if(!RegisterClassW(&cls))return 2;
    target=CreateWindowW(cls.lpszClassName,L"TinyTask 2.0 - Native routing experiment",WS_OVERLAPPEDWINDOW,40,80,650,360,nullptr,nullptr,instance,nullptr);
    work=CreateWindowExW(WS_EX_NOACTIVATE,cls.lpszClassName,L"TinyTask background work fixture",WS_OVERLAPPEDWINDOW,710,80,420,320,nullptr,nullptr,instance,nullptr);
    if(!target || !work)return 3;
    CreateWindowW(L"BUTTON",L"Start 20-second test",WS_CHILD|WS_VISIBLE|BS_PUSHBUTTON,20,270,220,35,target,reinterpret_cast<HMENU>(1),instance,nullptr);
    wchar_t exe[MAX_PATH]{};if(!GetModuleFileNameW(nullptr,exe,MAX_PATH))return 4;
    std::wstring command=L"\""+std::wstring(exe)+L"\" --raw-observer "+std::to_wstring(reinterpret_cast<ULONG_PTR>(target));
    STARTUPINFOW startup{};startup.cb=sizeof(startup);PROCESS_INFORMATION child{};
    if(!CreateProcessW(exe,command.data(),nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&startup,&child))return 8;
    observer=child.hProcess;observerThread=child.dwThreadId;CloseHandle(child.hThread);
    ShowWindow(target,SW_SHOW);ShowWindow(work,SW_SHOWNOACTIVATE);UpdateWindow(target);
    MSG message{};while(GetMessageW(&message,nullptr,0,0)>0){TranslateMessage(&message);DispatchMessageW(&message);}
    release();PostThreadMessageW(observerThread,WM_QUIT,0,0);if(WaitForSingleObject(observer,2000)==WAIT_TIMEOUT)TerminateProcess(observer,0);CloseHandle(observer);return 0;
}
