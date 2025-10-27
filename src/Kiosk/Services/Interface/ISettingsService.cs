using Kiosk.Models;
using Kiosk.ViewModels;

namespace Kiosk.Services.Interface;

public interface ISettingsService
{
    // 직원리스트
    Task<SettingsModel.StaffPageResult> GetUserListAsync(long hostLocationOid, int pageNo,
        CancellationToken ct = default);

    //직원 추가 
    Task<bool> Invite_StaffRegularAsync(long hostLocationOid, string phone, CancellationToken ct = default);

    //권한 변경 (Level)
    Task<bool> UpdateUserLevelAsync(long hostLocationOid, long userOid, string level, CancellationToken ct = default);

    //사용자 삭제
    Task<bool> DeleteUserAsync(long hostLocationOid, long userOid, CancellationToken ct = default); // level=-1 위임

    //직무 변경 (Role)
    Task<bool> UpdateUserRoleAsync(long hostLocationOid, long userOid, string role, CancellationToken ct = default);

    // 출퇴근 조회 (월 단위) -> 일 단위도 세부 조정가능
    Task<IReadOnlyList<SettingsModel.CommuteListModel>> GetDailyCommuteAsync(
        long hostLocationOid, int year, int month, int? day = null, long userOid = 0, CancellationToken ct = default);
}