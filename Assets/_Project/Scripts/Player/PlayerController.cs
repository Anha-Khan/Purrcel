using System;
using System.Collections;
using System.Linq;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Obstacles;
using CatCourier.Scoring;
using CatCourier.Weather;
using CatCourier.Packages;
using UnityEngine;

namespace CatCourier.Player
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private InputHandler input;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private BoxCollider2D hitbox;
        [SerializeField] private Animator animator;
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private WeatherManager weatherManager;
        [SerializeField] private PackageManager packageManager;
        [SerializeField] private bool useConfiguredWorldDeathY;
        [SerializeField] private float worldDeathY;

        private PlayerStats stats = new();
        private Vector2 originalSize;
        private Vector2 originalOffset;
        private bool grounded;
        private bool doubleJumpAvailable;
        private float verticalVelocity;
        private float runTime;
        private float jumpBufferTimer;
        private float coyoteTimer;
        private float slideTimer;
        private float wallBounceTimer;
        private float invincibilityTimer;
        private bool initialized;

        public PlayerState State { get; private set; } = PlayerState.Running;
        public bool IsGrounded => grounded;
        public bool IsInvincible => invincibilityTimer > 0f;
        public bool DoubleJumpAvailable => doubleJumpAvailable;
        public float CurrentSpeed => stats?.CurrentRunSpeed ?? Constants.BASE_RUN_SPEED;
        public float CurrentHorizontalVelocity => State == PlayerState.WallBounce ? Constants.WALL_BOUNCE_HORIZONTAL : CurrentSpeed;
        public float VerticalVelocity => verticalVelocity;
        public float RunTime => runTime;
        public float DistanceMeters => body != null && Application.isPlaying ? body.position.x : transform.position.x;
        public PlayerStats Stats => stats;
        public BoxCollider2D Hitbox => hitbox;
        public Func<long, int, RunResult> ResultFactory { get; set; }

        public event Action OnJump;
        public event Action OnDoubleJump;
        public event Action OnSlide;
        public event Action OnWallBounce;
        public event Action OnDeath;
        public event Action<float> OnLand;
        public event Action<float, float> OnScreenShakeRequested;

        private void Awake()
        {
            Initialize();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            stats ??= new PlayerStats();
            input ??= GetComponent<InputHandler>();
            body ??= GetComponent<Rigidbody2D>();
            hitbox ??= GetComponent<BoxCollider2D>();
            animator ??= GetComponentInChildren<Animator>();
            scoreManager ??= GetComponent<ScoreManager>();
            coinManager ??= GetComponent<CoinManager>();
            weatherManager ??= GetComponent<WeatherManager>();
            packageManager ??= GetComponent<PackageManager>();

            if (hitbox != null)
            {
                originalSize = hitbox.size;
                originalOffset = hitbox.offset;
                hitbox.isTrigger = true;
            }

            if (input != null)
            {
                input.JumpPressed -= RequestJump;
                input.JumpPressed += RequestJump;
                input.SlidePressed -= RequestSlide;
                input.SlidePressed += RequestSlide;
                input.PausePressed -= TogglePause;
                input.PausePressed += TogglePause;
            }

            if (coinManager != null)
            {
                coinManager.Configure(stats.PremiumCoinMultiplier, stats.UpgradeCoinMultiplier, stats.BreedCoinMultiplier, stats.CoinMagnetRadius);
                coinManager.OnCoinsCollected -= HandleCoinCollected;
                coinManager.OnCoinsCollected += HandleCoinCollected;
                coinManager.ResetRun();
            }

            BindRuntimeModifierEvents();

            if (scoreManager != null)
            {
                scoreManager.Initialize(GetComponent<DifficultyManager>());
                scoreManager.ResetRun();
            }

            UpdateAnimator();
        }

        private void BindRuntimeModifierEvents()
        {
            if (weatherManager != null)
            {
                weatherManager.OnWeatherChanged -= ApplyRuntimeModifiers;
                weatherManager.OnWeatherChanged += ApplyRuntimeModifiers;
            }

            if (packageManager != null)
            {
                packageManager.OnRunReset -= ApplyRuntimeModifiers;
                packageManager.OnRunReset += ApplyRuntimeModifiers;
                packageManager.OnPackageDelivered -= ApplyRuntimeModifiers;
                packageManager.OnPackageDelivered += ApplyRuntimeModifiers;
                packageManager.OnPackageLost -= ApplyRuntimeModifiers;
                packageManager.OnPackageLost += ApplyRuntimeModifiers;
            }
        }

        public void ConfigureRunStats(PlayerStats runStats)
        {
            stats = runStats ?? new PlayerStats();
            stats.SetRunTime(runTime);
            ApplyRuntimeModifiers();
            coinManager?.Configure(stats.PremiumCoinMultiplier, stats.UpgradeCoinMultiplier, stats.BreedCoinMultiplier, stats.CoinMagnetRadius);
        }

        public void ConfigureRunModifiers(WeatherManager weather, PackageManager packages)
        {
            weatherManager = weather;
            packageManager = packages;
            BindRuntimeModifierEvents();
            ApplyRuntimeModifiers();
        }

        private void ApplyRuntimeModifiers(WeatherType _)
        {
            ApplyRuntimeModifiers();
        }

        private void ApplyRuntimeModifiers()
        {
            var speed = (weatherManager?.SpeedMultiplier ?? 1f) * (packageManager?.SpeedMultiplier ?? 1f);
            var jump = packageManager?.JumpMultiplier ?? 1f;
            stats.SetExternalMultipliers(speed, jump);
        }

        public void ConfigureWorldDeathY(float value)
        {
            worldDeathY = value;
            useConfiguredWorldDeathY = true;
        }

        public void ResetRun()
        {
            State = PlayerState.Running;
            grounded = false;
            doubleJumpAvailable = false;
            verticalVelocity = 0f;
            runTime = 0f;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            slideTimer = 0f;
            wallBounceTimer = 0f;
            invincibilityTimer = 0f;
            stats.SetRunTime(0f);
            ApplyRuntimeModifiers();
            input?.SetInputEnabled(isActiveAndEnabled);
            coinManager?.ResetRun();
            scoreManager?.ResetRun();
            RestoreHitbox();
            UpdateAnimator();
        }

        public void RespawnFromContinue()
        {
            State = PlayerState.Running;
            grounded = false;
            doubleJumpAvailable = false;
            verticalVelocity = 0f;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            slideTimer = 0f;
            wallBounceTimer = 0f;
            invincibilityTimer = 2f;
            RestoreHitbox();
            input?.SetInputEnabled(isActiveAndEnabled);
            UpdateAnimator();
        }

        private void OnDestroy()
        {
            if (input != null)
            {
                input.JumpPressed -= RequestJump;
                input.SlidePressed -= RequestSlide;
                input.PausePressed -= TogglePause;
            }

            if (coinManager != null)
            {
                coinManager.OnCoinsCollected -= HandleCoinCollected;
            }

            if (weatherManager != null)
            {
                weatherManager.OnWeatherChanged -= ApplyRuntimeModifiers;
            }

            if (packageManager != null)
            {
                packageManager.OnRunReset -= ApplyRuntimeModifiers;
                packageManager.OnPackageDelivered -= ApplyRuntimeModifiers;
                packageManager.OnPackageLost -= ApplyRuntimeModifiers;
            }
        }

        private static bool CanAcceptGameplayInput()
        {
            return GameManager.Instance == null || GameManager.Instance.State == GameState.Running;
        }

        private void TogglePause()
        {
            if (GameManager.Instance == null || State == PlayerState.Dead)
            {
                return;
            }

            if (GameManager.Instance.State == GameState.Running)
            {
                GameManager.Instance.PauseRun();
            }
            else if (GameManager.Instance.State == GameState.Paused)
            {
                GameManager.Instance.ResumeRun();
            }
        }

        private void FixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }

        public void Simulate(float deltaTime)
        {
            if (State == PlayerState.Dead)
            {
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            invincibilityTimer = Mathf.Max(0f, invincibilityTimer - deltaTime);
            runTime += deltaTime;
            stats.SetRunTime(runTime);

            if (jumpBufferTimer > 0f)
            {
                if (State == PlayerState.Sliding)
                {
                    EndSlide();
                    PerformJump(false);
                }
                else if (grounded || coyoteTimer > 0f)
                {
                    PerformJump(false);
                }
                else if (State == PlayerState.Jumping && doubleJumpAvailable)
                {
                    PerformJump(true);
                }
            }

            var displacement = Vector3.right * (CurrentHorizontalVelocity * deltaTime);
            if (State == PlayerState.Jumping)
            {
                displacement += Vector3.right * ((weatherManager?.AirborneDriftAcceleration ?? 0f) * deltaTime);
            }

            if (!grounded && (State == PlayerState.Jumping || State == PlayerState.WallBounce))
            {
                verticalVelocity += Constants.GRAVITY * deltaTime;
                verticalVelocity = Mathf.Max(Constants.MAX_FALL_SPEED, verticalVelocity);
                displacement += Vector3.up * (verticalVelocity * deltaTime);
            }

            ApplyDisplacement(displacement);
            scoreManager?.SetDistance(DistanceMeters);

            if (State == PlayerState.Sliding)
            {
                slideTimer -= deltaTime;
                if (slideTimer <= 0f)
                {
                    EndSlide();
                }
            }
            else if (State == PlayerState.WallBounce)
            {
                wallBounceTimer -= deltaTime;
                if (wallBounceTimer <= 0f)
                {
                    State = PlayerState.Jumping;
                    UpdateAnimator();
                }
            }

            if (grounded && State != PlayerState.Sliding)
            {
                State = PlayerState.Running;
                verticalVelocity = 0f;
            }

            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
            coyoteTimer = Mathf.Max(0f, coyoteTimer - deltaTime);

            if (IsBelowWorld() && !IsInvincible)
            {
                TriggerDeath();
                return;
            }

            UpdateAnimator();
        }

        private void ApplyDisplacement(Vector3 displacement)
        {
            if (body == null)
            {
                transform.position += displacement;
                return;
            }

            if (Application.isPlaying)
            {
                body.MovePosition(body.position + (Vector2)displacement);
                return;
            }

            transform.position += displacement;
        }

        public void RequestJump()
        {
            if (State != PlayerState.Dead && CanAcceptGameplayInput())
            {
                jumpBufferTimer = Constants.JUMP_BUFFER_TIME;
            }
        }

        public void RequestSlide()
        {
            if (!CanAcceptGameplayInput() || State != PlayerState.Running || !grounded)
            {
                return;
            }

            State = PlayerState.Sliding;
            slideTimer = Constants.SLIDE_DURATION;
            if (hitbox != null)
            {
                hitbox.size = new Vector2(originalSize.x, Constants.SLIDE_HITBOX_HEIGHT_ABS);
                hitbox.offset = new Vector2(originalOffset.x, originalOffset.y - (originalSize.y - Constants.SLIDE_HITBOX_HEIGHT_ABS) * 0.5f);
            }

            OnSlide?.Invoke();
            UpdateAnimator();
        }

        public void NotifyGround()
        {
            grounded = true;
            coyoteTimer = Constants.COYOTE_TIME;
            if (State == PlayerState.Jumping || State == PlayerState.WallBounce)
            {
                var landingVelocity = verticalVelocity;
                var hardLanding = packageManager?.IsFragile == true && landingVelocity < Constants.FRAGILE_DEATH_THRESHOLD;
                State = PlayerState.Running;
                verticalVelocity = 0f;
                doubleJumpAvailable = false;
                OnLand?.Invoke(Constants.SHAKE_LAND_MAGNITUDE);
                OnScreenShakeRequested?.Invoke(Constants.SHAKE_LAND_MAGNITUDE, Constants.SHAKE_LAND_DURATION);
                UpdateAnimator();
                if (hardLanding)
                {
                    TriggerDeath();
                    return;
                }

                var landingSlide = weatherManager?.LandingSlideDistance ?? 0f;
                if (landingSlide > 0f)
                {
                    ApplyDisplacement(Vector3.right * landingSlide);
                }
            }
        }

        public void NotifyGroundExit()
        {
            if (!grounded)
            {
                return;
            }

            grounded = false;
            coyoteTimer = Constants.COYOTE_TIME;
            if (State == PlayerState.Sliding)
            {
                EndSlide();
            }

            if (State == PlayerState.Running)
            {
                State = PlayerState.Jumping;
                verticalVelocity = 0f;
                UpdateAnimator();
            }
        }

        public void NotifyWallBounce()
        {
            if (State == PlayerState.Dead)
            {
                return;
            }

            grounded = false;
            verticalVelocity = Constants.WALL_BOUNCE_VERTICAL;
            State = PlayerState.WallBounce;
            wallBounceTimer = Constants.WALL_BOUNCE_DURATION;
            doubleJumpAvailable = stats.DoubleJumpUnlocked;
            OnWallBounce?.Invoke();
            SetAnimatorTrigger("WallBounce");
            UpdateAnimator();
        }

        public void NotifyObstacle(bool bounce)
        {
            scoreManager?.RegisterObstacleContact();
            if (bounce)
            {
                NotifyWallBounce();
            }
            else
            {
                TriggerDeath();
            }
        }

        public void TriggerDeath()
        {
            if (State == PlayerState.Dead || IsInvincible)
            {
                return;
            }

            State = PlayerState.Dead;
            grounded = false;
            scoreManager?.RegisterDeath();
            input?.SetInputEnabled(false);
            SetAnimatorTrigger("Die");
            OnScreenShakeRequested?.Invoke(Constants.SHAKE_DEATH_MAGNITUDE, Constants.SHAKE_DEATH_DURATION);
            OnDeath?.Invoke();
            if (Application.isPlaying)
            {
                StartCoroutine(FinishDeath());
            }
        }

        private void PerformJump(bool doubleJump)
        {
            jumpBufferTimer = 0f;
            if (doubleJump)
            {
                verticalVelocity = 0f;
                verticalVelocity = stats.DoubleJumpForce;
                doubleJumpAvailable = false;
                OnDoubleJump?.Invoke();
                SetAnimatorTrigger("Jump");
            }
            else
            {
                verticalVelocity = stats.JumpForce;
                grounded = false;
                coyoteTimer = 0f;
                doubleJumpAvailable = stats.DoubleJumpUnlocked;
                OnJump?.Invoke();
                SetAnimatorTrigger("Jump");
            }

            State = PlayerState.Jumping;
            UpdateAnimator();
        }

        private void EndSlide()
        {
            if (State != PlayerState.Sliding)
            {
                return;
            }

            State = grounded ? PlayerState.Running : PlayerState.Jumping;
            RestoreHitbox();
            UpdateAnimator();
        }

        private void RestoreHitbox()
        {
            if (hitbox == null)
            {
                return;
            }

            hitbox.size = originalSize;
            hitbox.offset = originalOffset;
        }

        private void HandleCoinCollected(int rawValue, int awardedValue, Vector3 worldPosition)
        {
            scoreManager?.CollectCoin(rawValue, worldPosition.x);
        }

        private IEnumerator FinishDeath()
        {
            yield return new WaitForSeconds(1.2f);
            var score = scoreManager?.Score ?? 0;
            var coins = coinManager?.RunCoins ?? 0;
            var result = ResultFactory?.Invoke(score, coins) ?? new RunResult(score, DistanceMeters, 0, coins, DistrictId.OldTown);
            GameManager.Instance?.EndRun(result);
        }

        private bool IsBelowWorld()
        {
            return useConfiguredWorldDeathY && transform.position.y < worldDeathY;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var ground = other.GetComponent<GroundSurface>();
            var bounce = other.GetComponent<BounceObstacle>();
            var staticObstacle = other.GetComponent<StaticObstacle>();
            var stumble = other.GetComponent<StumbleObstacle>();
            if (ground != null)
            {
                NotifyGround();
            }
            else if (bounce != null)
            {
                NotifyObstacle(true);
            }
            else if (staticObstacle != null)
            {
                NotifyObstacle(false);
            }
            else if (stumble != null)
            {
                scoreManager?.RegisterObstacleContact();
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<GroundSurface>() != null)
            {
                NotifyGroundExit();
            }
        }

        private void UpdateAnimator()
        {
            if (animator == null)
            {
                return;
            }

            SetAnimatorFloat("Speed", CurrentSpeed);
            SetAnimatorBool("IsGrounded", grounded);
            SetAnimatorBool("IsSliding", State == PlayerState.Sliding);
        }

        private void SetAnimatorFloat(string name, float value)
        {
            if (animator.parameters.Any(parameter => parameter.name == name && parameter.type == AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(name, value);
            }
        }

        private void SetAnimatorBool(string name, bool value)
        {
            if (animator.parameters.Any(parameter => parameter.name == name && parameter.type == AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(name, value);
            }
        }

        private void SetAnimatorTrigger(string name)
        {
            if (animator != null && animator.parameters.Any(parameter => parameter.name == name && parameter.type == AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger(name);
            }
        }
    }
}
