namespace SamuraiRunner.Audio
{
    /// <summary>게임에서 쓰는 효과음 종류. 소리 파일은 Assets/Resources/Sfx/(아래 파일 이름).wav</summary>
    public enum SfxId
    {
        Swing,          // 사무라이 칼 휘두르기
        Parry,          // 칼로 쳐내기 (금속 부딪힘)
        JustParry,      // 저스트 패링 (맑고 길게 울림)
        Jump,           // 점프
        Land,           // 착지
        Footstep,       // 달리기 발소리
        Hurt,           // 피격
        Death,          // 쓰러짐
        Fall,           // 구덩이에 떨어짐
        Heal,           // 주먹밥으로 회복
        PickupDrop,     // 주먹밥이 떨어짐
        BowShot,        // 궁수가 활을 쏨
        ShurikenThrow,  // 닌자가 수리검을 던짐
        EnemyDeath,     // 적 처치
        LogSwing,       // 흔들리는 통나무가 풀려 내려옴
        LogHit,         // 통나무를 쳐내거나 통나무에 맞음 (나무 소리)
        SpeedUp,        // 속도 단계 상승
        UiStart,        // 시작 화면 START (칼 뽑는 소리)
        GameOver,       // 게임오버 징 소리
    }

    /// <summary>효과음 기본값 (SoundSettings 에셋이 없거나 항목이 빠졌을 때 사용).</summary>
    public static class SfxDefaults
    {
        public static string FileName(SfxId id) => id switch
        {
            SfxId.Swing => "swing",
            SfxId.Parry => "parry",
            SfxId.JustParry => "just_parry",
            SfxId.Jump => "jump",
            SfxId.Land => "land",
            SfxId.Footstep => "footstep",
            SfxId.Hurt => "hurt",
            SfxId.Death => "death",
            SfxId.Fall => "fall",
            SfxId.Heal => "heal",
            SfxId.PickupDrop => "pickup_drop",
            SfxId.BowShot => "bow_shot",
            SfxId.ShurikenThrow => "shuriken_throw",
            SfxId.EnemyDeath => "enemy_death",
            SfxId.LogSwing => "log_swing",
            SfxId.LogHit => "log_hit",
            SfxId.SpeedUp => "speed_up",
            SfxId.UiStart => "ui_start",
            SfxId.GameOver => "game_over",
            _ => id.ToString().ToLowerInvariant(),
        };

        /// <summary>
        /// 기본 볼륨·음높이 흔들림·재사용 대기시간. 볼륨은 소리마다 측정한 크기(RMS)에 맞춰
        /// 중요한 신호(쳐내기·피격·쓰러짐)는 크게, 자주 나는 소리(발소리·착지·점프)는 작게 잡았습니다.
        /// </summary>
        public static SfxEntry Create(SfxId id)
        {
            var e = new SfxEntry { id = id, volume = 1f, pitchJitter = 0f, cooldown = 0.03f };
            switch (id)
            {
                case SfxId.Swing: e.volume = 0.6f; e.pitchJitter = 0.06f; e.cooldown = 0.05f; break;
                case SfxId.Parry: e.volume = 1f; e.pitchJitter = 0.04f; break;
                case SfxId.JustParry: e.volume = 1f; e.pitchJitter = 0.02f; break;
                case SfxId.Jump: e.volume = 0.3f; e.pitchJitter = 0.04f; break;
                case SfxId.Land: e.volume = 0.2f; e.pitchJitter = 0.05f; e.cooldown = 0.08f; break;
                case SfxId.Footstep: e.volume = 0.2f; e.pitchJitter = 0.08f; e.cooldown = 0.06f; break;
                case SfxId.Hurt: e.volume = 0.5f; e.pitchJitter = 0.04f; e.cooldown = 0.1f; break;
                case SfxId.Death: e.volume = 1f; break;
                case SfxId.Fall: e.volume = 0.3f; break;
                case SfxId.Heal: e.volume = 0.6f; break;
                case SfxId.PickupDrop: e.volume = 0.35f; e.pitchJitter = 0.05f; break;
                case SfxId.BowShot: e.volume = 1f; e.pitchJitter = 0.04f; break;
                case SfxId.ShurikenThrow: e.volume = 0.65f; e.pitchJitter = 0.05f; break;
                case SfxId.EnemyDeath: e.volume = 1f; e.pitchJitter = 0.05f; break;
                case SfxId.LogSwing: e.volume = 0.8f; e.pitchJitter = 0.03f; break;
                case SfxId.LogHit: e.volume = 0.6f; e.pitchJitter = 0.05f; break;
                case SfxId.SpeedUp: e.volume = 0.2f; break;
                case SfxId.UiStart: e.volume = 0.95f; break;
                case SfxId.GameOver: e.volume = 1f; break;
            }
            return e;
        }
    }
}
