#pragma once
#include <array>
#include <cstddef>
#include <cstdint>

namespace tinytask {
// Portable policy for a future per-device mouse filter. This does not hook Windows.
// The future kernel adapter must serialize EVERY call under the same spin lock,
// supply monotonic milliseconds, and map IDs to verified PnP device instances.
struct MousePacket {
    std::int32_t dx{},dy{};
    std::uint16_t buttonFlags{},buttonData{},flags{};
    std::uint16_t unitId{};
    std::uint32_t rawButtons{},extraInformation{};
};
class FilterPolicy {
    std::array<MousePacket,64> queue{};
    std::size_t head{},count{};
    std::uint64_t device{},owner{},renewed{};
    bool armed=false;
public:
    static constexpr std::uint64_t LeaseMs=500;
    void Stop() noexcept {armed=false;device=owner=renewed=0;head=count=0;}
    bool Arm(std::uint64_t selected,std::uint64_t session,std::uint64_t now,
             bool physicalButtonsReleased,bool windowsButtonsReleased) noexcept {
        if(armed || !selected || !session || !physicalButtonsReleased || !windowsButtonsReleased)return false;
        head=count=0;device=selected;owner=session;renewed=now;armed=true;return true;
    }
    bool Active(std::uint64_t now) noexcept {
        if(armed && (now<renewed || now-renewed>=LeaseMs))Stop();
        return armed;
    }
    bool Renew(std::uint64_t session,std::uint64_t now) noexcept {
        if(!Active(now) || session!=owner)return false;
        renewed=now;return true;
    }
    // True means the adapter must forward this packet to Windows unchanged.
    // Overflow/lease expiry deliberately restore normal input instead of dropping it.
    bool Forward(std::uint64_t source,const MousePacket& packet,std::uint64_t now) noexcept {
        if(!Active(now) || source!=device)return true;
        if(count==queue.size()){Stop();return true;}
        queue[(head+count)%queue.size()]=packet;++count;return false;
    }
    bool Take(std::uint64_t session,MousePacket& packet,std::uint64_t now) noexcept {
        if(!Active(now) || session!=owner || !count)return false;
        packet=queue[head];head=(head+1)%queue.size();--count;return true;
    }
    void Disconnect(std::uint64_t source) noexcept {if(source==device)Stop();}
    void Close(std::uint64_t session) noexcept {if(session==owner)Stop();}
};
}
