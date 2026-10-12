// Independently authored ABI fixture. No upstream headers or implementation.
#define WIN32_LEAN_AND_MEAN
#define _WIN32_WINNT 0x0601
#define NTDDI_VERSION 0x06010000
#include <winsock2.h>
#include <ws2ipdef.h>
#include <windows.h>
#include <iphlpapi.h>
#include <netioapi.h>
#include <string>

struct Host {
    virtual int api() = 0; virtual const wchar_t* version() = 0;
    virtual double value(int) = 0; virtual const wchar_t* text(int,int) = 0;
    virtual void notify(const wchar_t*) = 0; virtual unsigned short language() = 0;
    virtual const wchar_t* config() = 0; virtual int dpi(int) = 0; virtual unsigned color() = 0;
    virtual const wchar_t* resource(const wchar_t*,const wchar_t*) = 0;
    virtual void* mainWindow() = 0; virtual void* taskbarWindow() = 0;
};
static Host* host;
struct Drawer {
    virtual int api()=0; virtual void text(int,int,int,int,const wchar_t*,unsigned,int,bool,unsigned char)=0;
    virtual void clip(int,int,int,int)=0; virtual void fill(int,int,int,int,unsigned,unsigned char)=0;
    virtual void outline(int,int,int,int,unsigned,int,bool,unsigned char,int)=0;
    virtual void bitmap(void*,int,int,int,int,int,unsigned char)=0; virtual void icon(void*,int,int,int,int)=0;
    virtual void extent(const wchar_t*,int&,int&)=0; virtual void line(int,int,int,int,unsigned,int,bool,unsigned char)=0; virtual void* dc()=0;
};
static int mode, commandCount;
static bool monitorGood;
struct Item {
    int drawing;
    std::wstring sample;
    Item(int draw) : drawing(draw) {}
    virtual const wchar_t* name() { return drawing ? L"Fixture drawing" : L"Fixture value"; }
    virtual const wchar_t* id() { return drawing == 2 ? L"fixture.gdi" : drawing ? L"fixture.draw" : L"fixture.value"; }
    virtual const wchar_t* label() { return L"Counter: "; }
    virtual const wchar_t* value() { sample = monitorGood && host && host->value(0) == 12345 && host->api() == 1 && host->dpi(0) == 96 && host->config()[0] ? L"verified" : monitorGood && !host ? L"legacy" : L"invalid"; return sample.c_str(); }
    virtual const wchar_t* example() { return L"999"; }
    virtual bool custom() { return drawing; }
    virtual int width() { return 120; }
    virtual void draw(void* dc, int x, int y, int w, int h, bool dark) {
        RECT r{x,y,x+w,y+h}; HBRUSH b = CreateSolidBrush(RGB(20,180,100)); FillRect((HDC)dc,&r,b); DeleteObject(b);
    }
    virtual int widthEx(void*) { return 120; }
    virtual int mouse(int,int,int,void*,int) { return 0; }
    virtual int keyboard(int,bool,bool,bool,void*,int) { return 0; }
    virtual void* itemInfo(int,void*,void*) { return nullptr; }
    virtual int graph() { return 0; }
    virtual float graphValue() { return 0; }
    virtual bool drawEx(void* drawer,int x,int y,int w,int h,bool) {
        if (drawing != 1) return false;
        auto d = (Drawer*)drawer; if (d->api()!=1) return false;
        int tw=0,th=0; d->extent(L"API 8",tw,th); if(tw<1||th<1||!d->dc()) return false;
        d->clip(x,y,w,h); d->fill(x,y,w,h,0x14b464,255); d->outline(x,y,w-1,h-1,0xffffff,1,false,255,4);
        d->text(x,y,w,h,L"API 8",0xffffff,0,false,255); d->line(0,h-2,w,h-2,0xffffff,1,true,200);
        HDC dc=(HDC)d->dc(); HBITMAP bm=CreateCompatibleBitmap(dc,8,8); d->bitmap(bm,w-12,4,8,8,2,128); DeleteObject(bm);
        d->icon(LoadIcon(nullptr,IDI_INFORMATION),w-28,8,16,16); return true;
    }
    virtual int exclusive() { return 0; }
};
static Item first(0), second(1), third(2);
struct Plugin {
    virtual int api() { wchar_t value[16]{}; return GetEnvironmentVariableW(L"TRAFFICSENSE_FIXTURE_API",value,16) ? _wtoi(value) : 8; }
    virtual Item* item(int index) { return index == 0 ? &first : index == 1 ? &second : index == 2 ? &third : nullptr; }
    virtual void sample() { if (mode == 1) Sleep(INFINITE); if (mode == 2) TerminateProcess(GetCurrentProcess(), 77); }
    virtual int options(void*) { return 1; }
    virtual const wchar_t* info(int) { return L"TrafficSense ABI fixture"; }
    virtual void monitor(const void* pointer) { struct Info { unsigned long long up,down; int cpu,memory,gpu,disk,cpuTemp,gpuTemp,diskTemp,boardTemp,freq; }; auto info=(const Info*)pointer; monitorGood=info && info->up==12345 && info->cpu==-1 && info->freq==-1; }
    virtual const wchar_t* tooltip() { return L"Test-only fixture"; }
    virtual void extended(int, const wchar_t*) {}
    virtual void* icon() { return nullptr; }
    virtual int commands() { return 3; }
    virtual const wchar_t* commandName(int i) { return i == 0 ? L"Hang" : i == 1 ? L"Crash" : L"Count"; }
    virtual void* commandIcon(int) { return nullptr; }
    virtual void command(int i, void*, void*) { mode = i == 0 ? 1 : i == 1 ? 2 : 0; commandCount++; }
    virtual int checked(int) { return 0; }
    virtual void initialize(Host* value) { host = value; }
};
static Plugin plugin;
extern "C" __declspec(dllexport) Plugin* TMPluginGetInstance() { return &plugin; }
extern "C" __declspec(dllexport) unsigned ReadNicCounters(const GUID* id, unsigned long long* rx, unsigned long long* tx) {
    MIB_IF_ROW2 row{}; unsigned error = ConvertInterfaceGuidToLuid(id, &row.InterfaceLuid);
    if (error) return error;
    error = GetIfEntry2(&row); if (!error) { *rx = row.InOctets; *tx = row.OutOctets; }
    return error;
}
