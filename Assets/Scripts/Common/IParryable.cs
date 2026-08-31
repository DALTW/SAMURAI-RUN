using UnityEngine;

namespace SamuraiRunner.Common
{
    /// <summary>
    /// 칼에 베였을 때 반응해야 하는 오브젝트(화살·수리검 등 투사체)가 구현할 인터페이스.
    /// </summary>
    public interface IParryable
    {
        /// <param name="attacker">베기를 시전한 오브젝트(플레이어)</param>
        /// <param name="isJustParry">저스트 패링(빠듯한 타이밍) 성공 여부 — true면 반사, false면 쳐내기</param>
        void OnParried(GameObject attacker, bool isJustParry);
    }
}
