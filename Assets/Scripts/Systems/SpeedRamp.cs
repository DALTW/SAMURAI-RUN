using System;
using UnityEngine;
using SamuraiRunner.Player;
using SamuraiRunner.Audio;

namespace SamuraiRunner.Systems
{
    /// <summary>
    /// 달린 거리가 일정 거리(기본 50m)를 지날 때마다 플레이어 달리기 속도를 한 단계씩 올립니다.
    /// 단계가 오르면 목표 속도까지 짧게 가속해서 붙고, 최대 속도를 넘지 않습니다.
    /// 시작 속도는 게임 시작 시 PlayerMotor의 달리기 속도입니다.
    /// 수치는 Scene의 GameSystems에서 조절합니다.
    /// </summary>
    public class SpeedRamp : MonoBehaviour
    {
        [Header("연결 (비워두면 자동으로 찾음)")]
        public PlayerMotor playerMotor;
        public DistanceTracker distanceTracker;

        [Header("속도 증가")]
        [Tooltip("이 거리(m)를 지날 때마다 한 단계씩 빨라집니다")]
        [SerializeField] private float metersPerStep = 50f;
        [Tooltip("한 단계마다 늘어나는 달리기 속도 (유닛/초). 시작 속도 5 기준 0.5 = 10%")]
        [SerializeField] private float speedPerStep = 0.5f;
        [Tooltip("최대 달리기 속도 (유닛/초)")]
        [SerializeField] private float maxSpeed = 9f;
        [Tooltip("단계가 오를 때 목표 속도까지 붙는 가속도 (유닛/초²). 0이면 즉시 바뀜")]
        [SerializeField] private float acceleration = 2f;

        /// <summary>현재 단계 (0 = 시작 속도)</summary>
        public int Level { get; private set; }
        /// <summary>게임 시작 시 달리기 속도</summary>
        public float BaseSpeed { get; private set; }
        /// <summary>지금 단계의 목표 속도</summary>
        public float TargetSpeed => Mathf.Clamp(BaseSpeed + Level * speedPerStep, BaseSpeed, Mathf.Max(BaseSpeed, maxSpeed));

        /// <summary>단계가 바뀐 순간 (단계, 목표 속도)</summary>
        public event Action<int, float> LevelChanged;

        private void Start()
        {
            if (playerMotor == null) playerMotor = FindAnyObjectByType<PlayerMotor>();
            if (distanceTracker == null) distanceTracker = FindAnyObjectByType<DistanceTracker>();
            BaseSpeed = playerMotor != null ? playerMotor.RunSpeed : 0f;
        }

        private void Update()
        {
            if (playerMotor == null || distanceTracker == null || metersPerStep <= 0f) return;
            if (!playerMotor.AutoRun) return; // 죽어서 멈췄으면 그대로 둠

            int level = Mathf.FloorToInt(distanceTracker.DistanceMeters / metersPerStep);
            if (level != Level)
            {
                if (level > Level && TargetSpeed < Mathf.Max(BaseSpeed, maxSpeed)) Sfx.Play(SfxId.SpeedUp); // 최고 속도에 닿은 뒤엔 조용히
                Level = level;
                LevelChanged?.Invoke(Level, TargetSpeed);
            }

            float target = TargetSpeed;
            playerMotor.RunSpeed = acceleration <= 0f
                ? target
                : Mathf.MoveTowards(playerMotor.RunSpeed, target, acceleration * Time.deltaTime);
        }
    }
}
