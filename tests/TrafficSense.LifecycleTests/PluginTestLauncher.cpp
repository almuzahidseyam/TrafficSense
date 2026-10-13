// Test-only launcher: community dialogs stay on a desktop never shown to the
// user. The parent assigns this process to its kill-on-close job before "go".
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <string>
int wmain(int argc,wchar_t** argv) {
    if(argc!=3 || std::wstring(argv[1])!=L"--plugin-worker")return 90;
    wchar_t executable[32768]{},root[32768]{};
    if(!GetEnvironmentVariableW(L"TRAFFICSENSE_TEST_PROBE_EXE",executable,32768) || !GetEnvironmentVariableW(L"TRAFFICSENSE_PLUGIN_CONFIG_ROOT",root,32768))return 91;
    std::wstring go=std::wstring(root)+L"\\launcher-go";
    ULONGLONG deadline=GetTickCount64()+5000;
    while(GetFileAttributesW(go.c_str())==INVALID_FILE_ATTRIBUTES){if(GetTickCount64()>deadline)return 92;Sleep(10);}
    std::wstring name=L"TrafficSense.Test."+std::to_wstring(GetCurrentProcessId());
    HDESK desktop=CreateDesktopW(name.c_str(),nullptr,nullptr,0,GENERIC_ALL,nullptr);if(!desktop)return 93;
    HANDLE handles[]={GetStdHandle(STD_INPUT_HANDLE),GetStdHandle(STD_OUTPUT_HANDLE),GetStdHandle(STD_ERROR_HANDLE)};
    for(auto h:handles)if(!SetHandleInformation(h,HANDLE_FLAG_INHERIT,HANDLE_FLAG_INHERIT))return 94;
    STARTUPINFOEXW start{};start.StartupInfo.cb=sizeof(start);start.StartupInfo.lpDesktop=const_cast<wchar_t*>(name.c_str());
    start.StartupInfo.dwFlags=STARTF_USESTDHANDLES;start.StartupInfo.hStdInput=handles[0];start.StartupInfo.hStdOutput=handles[1];start.StartupInfo.hStdError=handles[2];
    SIZE_T bytes=0;InitializeProcThreadAttributeList(nullptr,1,0,&bytes);
    start.lpAttributeList=(LPPROC_THREAD_ATTRIBUTE_LIST)HeapAlloc(GetProcessHeap(),0,bytes);
    if(!start.lpAttributeList || !InitializeProcThreadAttributeList(start.lpAttributeList,1,0,&bytes) || !UpdateProcThreadAttribute(start.lpAttributeList,0,PROC_THREAD_ATTRIBUTE_HANDLE_LIST,handles,sizeof(handles),nullptr,nullptr))return 95;
    std::wstring command=L"\""+std::wstring(executable)+L"\" --plugin-worker \""+argv[2]+L"\"";
    PROCESS_INFORMATION process{};
    BOOL created=CreateProcessW(executable,&command[0],nullptr,nullptr,TRUE,EXTENDED_STARTUPINFO_PRESENT|CREATE_NO_WINDOW,nullptr,nullptr,&start.StartupInfo,&process);
    DeleteProcThreadAttributeList(start.lpAttributeList);HeapFree(GetProcessHeap(),0,start.lpAttributeList);
    if(!created){CloseDesktop(desktop);return 96;}
    CloseHandle(process.hThread);
    std::wstring pid=std::wstring(root)+L"\\worker-pid.txt",text=std::to_wstring(process.dwProcessId);
    HANDLE file=CreateFileW(pid.c_str(),GENERIC_WRITE,FILE_SHARE_READ,nullptr,CREATE_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);
    if(file!=INVALID_HANDLE_VALUE){DWORD written;WriteFile(file,text.data(),(DWORD)(text.size()*sizeof(wchar_t)),&written,nullptr);CloseHandle(file);}
    WaitForSingleObject(process.hProcess,INFINITE);DWORD code=97;GetExitCodeProcess(process.hProcess,&code);CloseHandle(process.hProcess);CloseDesktop(desktop);return (int)code;
}
