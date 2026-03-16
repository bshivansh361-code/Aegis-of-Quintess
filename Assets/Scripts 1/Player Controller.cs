using System;
using UnityEngine;
using System.Collections;
using DragonBones;
using UnityEngine.InputSystem; 

namespace TarodevController
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private ScriptableStats _stats;
        private Rigidbody2D _rb;
        private CapsuleCollider2D _col;
        private FrameInput _frameInput;
        private Vector2 _frameVelocity;
        private bool _cachedQueryStartInColliders;
        private int _jumpsRemaining;
        
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _dashAction; 
        private InputAction _aimAction;
        private InputAction _fireAction;
        
        private bool _facingRight = true;
        private bool _isDashing;
        private bool _dashToConsume;
        private float _dashTimeLeft;
        private float _lastDashTime = -100f;
        private float _time;

        #region Interface
        public Vector2 FrameInput => _frameInput.Move;
        public bool StickHeld => _frameInput.StickHeld;
        
        public bool IsWallLocked => _isWallLocked;
        public event System.Action<float> WallLockStarted;
        public event Action<bool, float> GroundedChanged;
        public event Action<bool> WallChanged;
        public event Action Jumped;
        public event Action Dashed; 
       
        
        private InputAction _stickAction;
        private bool _isWallSticking;
        private float _wallStickTimeLeft;
        private bool _isOnWall;
        private float _wallStickCooldownTimer;
        private bool _isWallLocked; 
        public float WallLockoutProgress { get; private set; }
        private bool _iswallJump;
     
        [Header("DragonBones Setup")]
        [SerializeField] string ikTargetBoneName ;
        private DragonBones.Bone _headBone;
        public UnityArmatureComponent armatureComponent;
    
        [Header("Aim Settings")]
        [Range(0.1f, 1.0f)] public float aimReach = 0.7f;
        private Bone _ikTargetBone;
        private Camera _mainCamera;
        [SerializeField] private bool _useController = true;
        
        // This is a "Getter" property. It runs the math every time you use the word 'MouseWorldPos'
        private Vector3 MouseWorldPos 
        {
            get 
            {
                if (UnityEngine.InputSystem.Mouse.current == null) return Vector3.zero;
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                // We use 10 for Z depth so ScreenToWorldPoint works in 2D
                Vector3 worldPos = _mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 10));
                worldPos.z = 0;
                return worldPos;
            }
        }

        private Vector2 DirectionToMouse => (MouseWorldPos - transform.position).normalized;
        #endregion

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
            SetupInputActions();
            _mainCamera = Camera.main;
        }

        private void SetupInputActions()
        {
            _moveAction = new InputAction("Move", type: InputActionType.Value);
            _moveAction.AddBinding("<Gamepad>/leftStick");
            _moveAction.AddCompositeBinding("Dpad")
                .With("Up", "<Keyboard>/w").With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a").With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d").With("Right", "<Keyboard>/rightArrow");

            _jumpAction = new InputAction("Jump", type: InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");
        

            _dashAction = new InputAction("Dash", type: InputActionType.Button);
            _dashAction.AddBinding("<Keyboard>/leftShift");
            _dashAction.AddBinding("<Gamepad>/rightShoulder"); // R1
            
            _stickAction = new InputAction("Stick", type: InputActionType.Button);
            _stickAction.AddBinding("<Keyboard>/space");
            _stickAction.AddBinding("<Gamepad>/buttonSouth");
            
            _aimAction = new InputAction("Aim", type: InputActionType.Value);
            _aimAction.AddBinding("<Gamepad>/rightStick");
          

            // 2. FIRE ACTION (Right Trigger and Left Mouse Button)
            _fireAction = new InputAction("Fire", type: InputActionType.Button);
            _fireAction.AddBinding("<Gamepad>/rightTrigger");
            _fireAction.AddBinding("<Mouse>/LeftButton");
      
        }

        private void OnEnable() { _moveAction.Enable(); _jumpAction.Enable(); _dashAction.Enable(); _stickAction.Enable(); _aimAction.Enable();_fireAction.Enable(); }
        private void OnDisable() { _moveAction.Disable(); _jumpAction.Disable(); _dashAction.Disable(); _stickAction.Disable(); _aimAction.Disable(); _fireAction.Disable(); }
        private void Start()
        {
            // Check 1: Is the component assigned in the inspector?
            if (armatureComponent == null)
            {
                Debug.LogError("DIAGNOSTIC: armatureComponent is completely empty! Assign it in the Inspector.");
                return;
            }

            // Check 2: Did DragonBones build the rig?
            if (armatureComponent.armature == null)
            {
                Debug.LogError("DIAGNOSTIC: armatureComponent exists, but the 'armature' inside it failed to build. Try re-importing the DragonBones data.");
                return;
            }

            // Check 3: Find the bones
            _ikTargetBone = armatureComponent.armature.GetBone(ikTargetBoneName);
            _headBone = armatureComponent.armature.GetBone("Head");

            // Check 4: Did we actually find them?
            if (_ikTargetBone == null) 
                Debug.LogError("DIAGNOSTIC: Could not find a bone named '" + ikTargetBoneName + "'. Check your spelling in DragonBones!");
    
            if (_headBone == null) 
                Debug.LogError("DIAGNOSTIC: Could not find a bone named 'Head'. Check your spelling in DragonBones!");

            if (_ikTargetBone != null && _headBone != null)
            {
                Debug.Log("DIAGNOSTIC: SUCCESS! Bones found and connected.");
            }
        }
        private void Update()
        {
            _time += Time.deltaTime;
            GatherInput();
      
            if (_iswallJump)
            {
                Debug.Log("Wall Jump");
            }
          
        }
        
        void LateUpdate()
        {
            Vector2 stickInput = _aimAction.ReadValue<Vector2>();
    
            // If the stick is being pushed past the deadzone, switch to Controller
            if (stickInput.sqrMagnitude > 0.1f) 
            {
                _useController = true;
            }
            // If the mouse moves significantly, switch back to Mouse
            // (We check for movement so simply having the mouse plugged in doesn't trigger it)
            else if (UnityEngine.InputSystem.Mouse.current.delta.ReadValue().sqrMagnitude > 0.1f)
            {
                _useController = false;
            }
            bool isFiring =  _fireAction.IsPressed();

            if (isFiring)
            {
                if (_useController)
                {
                    HandleControllerAiming();
                }
                else
                {
                    HandleArmAiming(); // This is your Mouse function
                }
        
                HandleSpriteFlip(true);
            }
            else
            { 
                if (_ikTargetBone != null && _headBone != null)
                {
                    _ikTargetBone.offset.x = 0;
                    _ikTargetBone.offset.y = 0;
                    _headBone.offset.rotation = 0;
        
                    // Refresh the skeleton
                    _ikTargetBone.InvalidUpdate();
                }
    
                // We still want to handle sprite flipping even if the bones are missing
                HandleSpriteFlip(false);
            }
        }

        private void HandleArmAiming()
        {
            if (_ikTargetBone == null) return;
            float facingDir = armatureComponent.transform.localScale.x;
            float angle = Mathf.Atan2(DirectionToMouse.y, DirectionToMouse.x * facingDir) * Mathf.Rad2Deg;
            float clampedAngle = Mathf.Clamp(angle, -70f, 70f);

            float rad = clampedAngle * Mathf.Deg2Rad;
            float distance = Vector2.Distance(transform.position, MouseWorldPos) * aimReach;
    
            Vector3 clampedLocalPos = new Vector3(Mathf.Cos(rad) * distance, Mathf.Sin(rad) * distance, 0);

            if (_headBone == null) return;
            float HeadclampedAngle = Mathf.Clamp(angle, -15f, 15f);
            _headBone.offset.rotation = HeadclampedAngle * -Mathf.Deg2Rad;
            
            _ikTargetBone.offset.x = clampedLocalPos.x;
            _ikTargetBone.offset.y = -clampedLocalPos.y; 
            _ikTargetBone.InvalidUpdate();
        }
        private void HandleControllerAiming()
        {
            if (_ikTargetBone == null || _headBone == null) return;

            // 1. Read Right Stick Value
            Vector2 stickInput = _aimAction.ReadValue<Vector2>();

            // 2. Deadzone Check
            // If stick isn't being moved enough, we don't update to avoid drift
            if (stickInput.sqrMagnitude < 0.1f) return;

            // 3. The Compass Math (Relative to Face)
            float facingDir = armatureComponent.transform.localScale.x;
    
            // Joystick gives us x and y directly!
            float angle = Mathf.Atan2(stickInput.y, stickInput.x * facingDir) * Mathf.Rad2Deg;

            // 4. Biological Clamping
            float armAngle = Mathf.Clamp(angle, -70f, 70f);
            float headAngle = Mathf.Clamp(angle, -15f, 15f);

            // 5. Reconstruction (Polar to Cartesian)
            float rad = armAngle * Mathf.Deg2Rad;
    
            // Joystick magnitude determines how far the arm reaches
            float reachDistance = stickInput.magnitude * 150;

            Vector3 armPos = new Vector3(
                Mathf.Cos(rad) * reachDistance, 
                Mathf.Sin(rad) * reachDistance, 
                0
            );

            // 6. Execution
            _headBone.offset.rotation = headAngle * -Mathf.Deg2Rad;
            _ikTargetBone.offset.x = armPos.x;
            _ikTargetBone.offset.y = -armPos.y; // Correcting DragonBones Y-axis

            _ikTargetBone.InvalidUpdate();
        }
    
        private void GatherInput()
        {
            _frameInput = new FrameInput
            {
                JumpDown = _jumpAction.WasPressedThisFrame(),
                JumpHeld = _jumpAction.IsPressed(),
                DashDown = _dashAction.WasPressedThisFrame(),
                StickHeld = _stickAction.IsPressed(),
                Move = _moveAction.ReadValue<Vector2>()
            };

            if (_stats.SnapInput)
            {
                _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.x);
                _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold ? 0 : Mathf.Sign(_frameInput.Move.y);
            }

            if (_frameInput.JumpDown)
            {
                _jumpToConsume = true;
                _timeJumpWasPressed = _time;
            }
            if (_frameInput.DashDown)
            {
                _dashToConsume = true; 
            }
        }

        private void FixedUpdate()
        {
            CheckCollisions();
            if (_wallStickCooldownTimer > 0) _wallStickCooldownTimer -= Time.fixedDeltaTime;

            if (_isDashing)
            {
                ContinueDash();
            }
            else if (_isWallSticking)
            {
                HandleWallStick();
            }
            else
            {
                // Start sticking if we are against a wall and holding the button
                if (_frameInput.StickHeld && _isOnWall && !_grounded&& !_isWallLocked)
                {
                    StartWallStick();
                }

                if (_dashToConsume && CanDash) 
                {
                    StartDash();
                    _dashToConsume = false;
                }
        
                HandleJump();
                HandleDirection();
                HandleGravity();
            }
            
            ApplyMovement();
            
           
        }

        private void HandleSpriteFlip(bool manualAiming)
        {
            if (_isDashing || _isWallSticking) return;

            float currentFacing = transform.localScale.x;

            if (manualAiming)
            {
                float aimDirectionX = 0;

                if (_useController)
                {
                    // MODE A: Controller - Read the Right Stick directly
                    Vector2 stickInput = _aimAction.ReadValue<Vector2>();
            
                    // We use a small deadzone (0.1f) to prevent accidental flips from stick drift
                    if (stickInput.sqrMagnitude > 0.01f) 
                    {
                        aimDirectionX = stickInput.x;
                    }
                    else
                    {
                        // If stick isn't moved, keep current facing to prevent snapping to right
                        aimDirectionX = currentFacing; 
                    }
                }
                else
                {
                    // MODE B: Mouse - Follow the cursor position relative to Naboo
                    aimDirectionX = MouseWorldPos.x - transform.position.x;
                }

                // Apply the flip if the aim direction is opposite to current facing
                // We use 0.1f as a threshold for the controller and 0.5f for the mouse to keep it stable
                float threshold = _useController ? 0.1f : 0.5f;

                if (Mathf.Abs(aimDirectionX) > threshold && Mathf.Sign(aimDirectionX) != Mathf.Sign(currentFacing))
                {
                    FlipLogic(-currentFacing);
                }
            }
            else
            {
                // MODE C: Normal Movement - Follow the A/D keys or Left Stick
                if (_frameInput.Move.x > 0 && currentFacing < 0) FlipLogic(1);
                else if (_frameInput.Move.x < 0 && currentFacing > 0) FlipLogic(-1);
            }
        }

        private void FlipLogic(float newScaleX)
        {
            transform.localScale = new Vector3(newScaleX, 1, 1);
            _facingRight = newScaleX > 0;
        }
        #region Collisions
        private float _frameLeftGrounded = float.MinValue;
        private bool _grounded;

        private void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;
            bool groundHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.down, _stats.GrounderDistance, ~_stats.PlayerLayer);
            float wallCheckDist = 0.1f;
            bool leftWall = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.left, wallCheckDist, ~_stats.PlayerLayer);
            bool rightWall = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.right, wallCheckDist, ~_stats.PlayerLayer);
            _isOnWall = leftWall || rightWall;
            
            // Reset stick if we leave the wall or touch the ground
            if ((!_isOnWall || groundHit) && _isWallSticking) 
            {
                _isWallSticking = false;
                WallChanged?.Invoke(false);
            }
            bool ceilingHit = Physics2D.CapsuleCast(_col.bounds.center, _col.size, _col.direction, 0, Vector2.up, _stats.GrounderDistance, ~_stats.PlayerLayer);

            if (ceilingHit) _frameVelocity.y = Mathf.Min(0, _frameVelocity.y);

            if (!_grounded && groundHit)
            {
                _grounded = true;
                _jumpsRemaining = _stats.MaxJumps;
                _coyoteUsable = true;
                _bufferedJumpUsable = true;
                _endedJumpEarly = false;
                GroundedChanged?.Invoke(true, Mathf.Abs(_frameVelocity.y));
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                GroundedChanged?.Invoke(false, 0);
            }
            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }
        #endregion
        private void StartWallStick()
        {
            _isWallSticking = true;
            _wallStickTimeLeft = _stats.WallStickDuration;
            _frameVelocity = Vector2.zero; // Stop movement immediately
            _jumpsRemaining = _stats.MaxJumps; // Optional: Reset jumps on wall touch
            
            WallChanged?.Invoke(true);
        }

        private void HandleWallStick()
        {
            _wallStickTimeLeft -= Time.fixedDeltaTime;
            _frameVelocity = Vector2.zero;
            
            if (_wallStickTimeLeft <= 0 || !_frameInput.StickHeld )
            {
                
                _isWallSticking = false;
        
                // 2. Broadcast the change immediately
               
                WallChanged?.Invoke(false);
                // 3. Start the lockout so he CANNOT re-grab this frame
                StartCoroutine(WallLockoutRoutine());

          
                
            }
        }

        #region Dash
        private bool CanDash => _time > _lastDashTime + _stats.DashCooldown;

        private void StartDash()
        {
            _isDashing = true;
            _dashTimeLeft = _stats.DashDuration;
            _lastDashTime = _time;
            _frameVelocity = new Vector2((_facingRight ? 1 : -1) * _stats.DashVelocity, 0);
            Dashed?.Invoke();
        }

        private void ContinueDash()
        {
            _dashTimeLeft -= Time.fixedDeltaTime;
            if (_dashTimeLeft <= 0)
            {
                _isDashing = false;
                _frameVelocity.x *= 0.5f; 
            }
        }
        #endregion

        #region Jumping
        private bool _jumpToConsume;
        private bool _bufferedJumpUsable;
        private bool _endedJumpEarly;
        private bool _coyoteUsable;
        private float _timeJumpWasPressed;

        private bool HasBufferedJump => _bufferedJumpUsable && _time < _timeJumpWasPressed + _stats.JumpBuffer;
        private bool CanUseCoyote => _coyoteUsable && !_grounded && _time < _frameLeftGrounded + _stats.CoyoteTime;

        private void HandleJump()
        {
            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.linearVelocity.y > 0) _endedJumpEarly = true;
            if (!_jumpToConsume && !HasBufferedJump) return;
            if (_grounded || CanUseCoyote || _jumpsRemaining > 0)
            {
                // Special check: if we are in the air and not in Coyote time, 
                // and we only have 1 jump left, it means we are on our LAST jump.
                if (!_grounded && !CanUseCoyote && _jumpsRemaining == _stats.MaxJumps)
                {
                    // This prevents "skipping" a charge if you fall off a ledge 
                    // without jumping first.
                    _jumpsRemaining--; 
                }

                ExecuteJump();
            }

            _jumpToConsume = false;
        }

        private void ExecuteJump()
        {
          
            _jumpsRemaining--;
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            _frameVelocity.y = _stats.JumpPower;
            
         
            if (_jumpsRemaining > 1f)
            {
                Jumped?.Invoke();
            }
        
            
        }

        #endregion

        #region Horizontal
        private void HandleDirection()
        {
            float moveInput = _frameInput.Move.x;
            bool isAiming = _fireAction.IsPressed();

            // Only restrict movement if the player is actively aiming/firing
            if (isAiming)
            {
                float facingDir = transform.localScale.x;
                if (moveInput != 0 && Mathf.Sign(moveInput) != Mathf.Sign(facingDir))
                {
                    moveInput = 0; 
                }
            }

            if (moveInput == 0)
            {
                var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                // Use the filtered 'moveInput' here
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, moveInput * _stats.MaxSpeed, _stats.Acceleration * Time.fixedDeltaTime);
            }
        }
        #endregion

        #region Gravity
        private void HandleGravity()
        {
            if (_grounded && _frameVelocity.y <= 0f)
            {
                _frameVelocity.y = _stats.GroundingForce;
            }
            else
            {
                var inAirGravity = _stats.FallAcceleration;
                if (_endedJumpEarly && _frameVelocity.y > 0) inAirGravity *= _stats.JumpEndEarlyGravityModifier;
                _frameVelocity.y = Mathf.MoveTowards(_frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
        }
   
        #endregion

        private void ApplyMovement() => _rb.linearVelocity = _frameVelocity;
        
        
        private IEnumerator WallLockoutRoutine()
        {
            _isWallLocked = true;
            WallLockStarted?.Invoke(_stats.WallLockoutDuration);
            float elapsed = 0;

            while (elapsed < _stats.WallLockoutDuration)
            {
                elapsed += Time.deltaTime;
                // Progress for UI: 0 is empty (just started), 1 is full (ready to grab)
                WallLockoutProgress = elapsed / _stats.WallLockoutDuration;
                yield return null;
            }

            _isWallLocked = false;
            WallLockoutProgress = 1f;
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_stats == null) Debug.LogWarning("Please assign a ScriptableStats asset", this);
        }
#endif
    }

    public struct FrameInput
    {
        public bool JumpDown;
        public bool JumpHeld;
        public bool DashDown;
        public bool StickHeld;
        
        public Vector2 Move;
    }

    public interface IPlayerController
    {
        public event Action<bool, float> GroundedChanged;
        public event Action Jumped;
        public event Action Dashed;
        public event System.Action<float> WallLockStarted;
        event Action<bool> WallChanged;
    }
}