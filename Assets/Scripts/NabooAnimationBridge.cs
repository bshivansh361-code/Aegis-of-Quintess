using UnityEngine;
using TarodevController;
using DragonBones;
using System.Collections.Generic;


[System.Serializable]
public struct HaverinTier
{
    public string tierName; // Label for your own sanity (e.g., "Level 1 - Basic")
    public Sprite idleHaverinSprite; 
    [Header("Animation Names (Match DragonBones Exactly)")]
    public string runAnim;
    public string wallGrabAnim;
    public string takeOffAnim;
    public string midAirAnim;
    public string fallAnim;
    public string landAnim;
}

[RequireComponent(typeof(IPlayerController))]
[RequireComponent(typeof(Rigidbody2D))]


public class NabooAnimationBridge : MonoBehaviour
{
    private IPlayerController _player;
    private Rigidbody2D _rb; 

    [Header("Visual References")]
    public UnityArmatureComponent armature;
    public GameObject idleVisualsObject; 

    [Header("Equipment Tiers")]
    [Tooltip("Add up to 6 tiers here to match keys 1-6")]
    public List<HaverinTier> equipmentTiers = new List<HaverinTier>();
    
  
    
    private int _currentTierIndex = 0;
    private string _currentState = "";
    private bool _isGrounded = true;
    private bool _isWallSticking = false;

    public SpriteRenderer idleHaverinRenderer;
    // Helper to get the strings for the active level
    private HaverinTier CurrentTier => equipmentTiers[_currentTierIndex];

    private void Awake()
    {
        _player = GetComponent<IPlayerController>();
        _rb = GetComponent<Rigidbody2D>(); 
       
    }

    private void OnEnable()
    {
        _player.Jumped += PlayTakeOff;
        _player.GroundedChanged += OnGroundedChanged;
        _player.WallChanged += OnWallChanged;
    }

    private void OnDisable()
    {
        _player.Jumped -= PlayTakeOff;
        _player.GroundedChanged -= OnGroundedChanged;
        _player.WallChanged -= OnWallChanged;
    }

private void Update()
    {
        #region Debug Feature - New Input System Direct Access
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SetTier(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SetTier(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SetTier(2);
            if (keyboard.digit4Key.wasPressedThisFrame) SetTier(3);
            if (keyboard.digit5Key.wasPressedThisFrame) SetTier(4);
            if (keyboard.digit6Key.wasPressedThisFrame) SetTier(5);
        }
        #endregion

        HandleAnimations();
    }
   
    private void SetTier(int index)
    {
        if (index >= 0 && index < equipmentTiers.Count)
        {
            _currentTierIndex = index;
            _currentState = ""; // Reset so the new tier's animation triggers immediately
            if (idleHaverinRenderer != null)
            {
                idleHaverinRenderer.sprite = CurrentTier.idleHaverinSprite;
            }
            Debug.Log($"<color=cyan>Haverin Swapped:</color> {CurrentTier.tierName} active.");
        }
        else
        {
            Debug.LogWarning($"Attempted to swap to Tier Index {index}, but the list only has {equipmentTiers.Count} items!");
        }
    }

    private void HandleAnimations()
    {
        // 1. WALL STICKING (Highest Priority)
        if (_isWallSticking)
        {
            // Safety release: if we accidentally hit the ground or move up, kill the grab
            if (_isGrounded || _rb.linearVelocity.y > 0.1f)
            {
                _isWallSticking = false;
                _currentState = "";
            }
            else
            {
                SetVisualMode(true); // Armature ON
                PlayDragonBonesAnim(CurrentTier.wallGrabAnim);
                return;
            }
        }

        // 2. GROUNDED (Running vs Idle)
        if (_isGrounded)
        {
            _isWallSticking = false;
            
            // If moving horizontally and NOT moving vertically (prevent run anim while jumping)
            if (Mathf.Abs(_rb.linearVelocity.x) > 0.1f && Mathf.Abs(_rb.linearVelocity.y) < 0.1f)
            {
                SetVisualMode(true);
                PlayDragonBonesAnim(CurrentTier.runAnim);
            }
            else 
            {
                // Switch to your Idle Object (Static Sprite or different armature)
                SetVisualMode(false);
                _currentState = ""; 
            }
            return;
        }

        // 3. AIRBORNE (Jump & Fall)
        SetVisualMode(true);

        // Don't interrupt the TakeOff wind-up
        if (armature.animation.lastAnimationName == CurrentTier.takeOffAnim && !armature.animation.isCompleted)
        {
            return;
        }

        if (_rb.linearVelocity.y > 0.1f)
            PlayDragonBonesAnim(CurrentTier.midAirAnim);
        else
            PlayDragonBonesAnim(CurrentTier.fallAnim);
    }

    private void SetVisualMode(bool useArmature)
    {
        if (armature != null) armature.gameObject.SetActive(useArmature);
        if (idleVisualsObject != null) idleVisualsObject.SetActive(!useArmature);
    }

    private void PlayTakeOff()
    {
        // If we jump while running, skip the squat and go straight to MidAir
        if (Mathf.Abs(_rb.linearVelocity.x) > 0.1f)
        {
            PlayDragonBonesAnim(CurrentTier.midAirAnim);
        }
        else
        {
            PlayDragonBonesAnim(CurrentTier.takeOffAnim, 1); // Play once
        }
    }

    private void OnWallChanged(bool isSticking)
    {
        _isWallSticking = isSticking;
        _currentState = ""; // Force a re-evaluation
    }

    private void OnGroundedChanged(bool grounded, float impactVelocity)
    {
        _isGrounded = grounded;
        if (grounded)
        {
            PlayDragonBonesAnim(CurrentTier.landAnim, 1); 
        }
    }

    private void PlayDragonBonesAnim(string animName, int playTimes = 0)
    {
        if (string.IsNullOrEmpty(animName)) return;
        if (_currentState == animName) return;

        // Verification check: Is the name correct?
        if (armature.armature.animation.HasAnimation(animName))
        {
            _currentState = animName;
            armature.animation.FadeIn(animName, -1, playTimes);
        }
        else
        {
            Debug.LogError($"<color=red>Missing Animation:</color> '{animName}' not found in DragonBones! Check your Inspector list.");
        }
    }
}