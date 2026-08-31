using UnityEngine;
using UnityEngine.InputSystem;

namespace SamuraiRunner.Player
{
    /// <summary>
    /// 키보드 입력만 담당하는 스크립트. 조작키는 Scene의 Inspector에서 바꿀 수 있습니다.
    /// 기본: Z = 점프, X = 베기
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("조작키 (Scene에서 변경 가능)")]
        [SerializeField] private Key jumpKey = Key.Z;
        [SerializeField] private Key attackKey = Key.X;

        public bool JumpPressed => KeyDown(jumpKey);
        public bool JumpReleased => KeyUp(jumpKey);
        public bool JumpHeld => KeyHeld(jumpKey);
        public bool AttackPressed => KeyDown(attackKey);

        private static bool KeyDown(Key key) =>
            key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;

        private static bool KeyUp(Key key) =>
            key != Key.None && Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame;

        private static bool KeyHeld(Key key) =>
            key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed;
    }
}
