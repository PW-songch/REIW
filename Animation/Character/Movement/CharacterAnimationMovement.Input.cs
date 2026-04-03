using UnityEngine;
using System.Collections.Generic;

namespace REIW.Animations.Character
{
    public partial class CharacterAnimationMovement
    {
        [Header("InputBuffer Settings")]
        [SerializeField] private CharacterActionInputBuffer _actionInputBuffer;

        public eCharacterActionInputType CurrentActionInputType => _actionInputBuffer.hasBuffer ? _actionInputBuffer.value : eCharacterActionInputType.NONE;
        public bool IsAnyMovementInput => IsMoveInput || IsAnyActionInput;
        public bool IsAnyActionInput => CurrentActionInputType != eCharacterActionInputType.NONE;
        public bool IsCancelCurrentActionInput => IsDashInput;
        public bool IsMoveInput => !Mathf.Approximately(MovementDirection.sqrMagnitude, 0f);
        public bool IsActualMoveInput => !Mathf.Approximately(InputController.Singleton.Move.sqrMagnitude, 0f);
        public bool IsWalkInput { get; set; }

        private Dictionary<eCharacterActionInputType, System.Action<bool>> _actionInputBufferActions = new();

        public void AddInputBufferAction(eCharacterActionInputType inputtype, System.Action<bool> inputaction)
        {
            if (!_actionInputBufferActions.TryGetValue(inputtype, out var existing) || existing == null)
            {
                _actionInputBufferActions[inputtype] = inputaction;  // 최초 등록
            }
            else
            {
                _actionInputBufferActions[inputtype] -= inputaction;
                _actionInputBufferActions[inputtype] += inputaction;
            }
        }

        private bool this[eCharacterActionInputType type]
        {
            get => CurrentActionInputType == type;
            set
            {
                if (value)
                {
                    _actionInputBuffer.Set(type);
                }
                else if (_actionInputBuffer.value == type)
                {
                    _actionInputBuffer.Set(eCharacterActionInputType.NONE);
                    _actionInputBuffer.Reset();
                }

                if (_actionInputBufferActions.TryGetValue(type, out var action) && action != null)
                    action(value);
            }
        }

        public bool IsSprintInput
        {
            get => _isSprintInput;
            set
            {
                _isSprintInput = value;
                if (!_isSprintInput)
                {
                    IsSprintInputBuffer = false;
                }
            }
        }

        public bool IsSprintInputBuffer
        {
            get => this[eCharacterActionInputType.SPRINT];
            set => this[eCharacterActionInputType.SPRINT] = value;
        }

        public bool IsDashInput
        {
            get => this[eCharacterActionInputType.DASH];
            set => this[eCharacterActionInputType.DASH] = value;
        }

        public bool IsJumpInput
        {
            get => this[eCharacterActionInputType.JUMP];
            set => this[eCharacterActionInputType.JUMP] = value;
        }

        public bool IsGrappleInput
        {
            get => this[eCharacterActionInputType.GRAPPLE];
            set => this[eCharacterActionInputType.GRAPPLE] = value;
        }
        
        public bool IsMountInput
        {
            get => this[eCharacterActionInputType.MOUNT];
            set => this[eCharacterActionInputType.MOUNT] = value;
        }

        public bool IsInteractionInput
        {
            get => this[eCharacterActionInputType.INTERACTION];
            set => this[eCharacterActionInputType.INTERACTION] = value;
        }

        public bool IsGatheringInput
        {
            get => this[eCharacterActionInputType.GATHERING];
            set => this[eCharacterActionInputType.GATHERING] = value;
        }

        public bool IsFishingInput
        {
            get => this[eCharacterActionInputType.FISHING];
            set => this[eCharacterActionInputType.FISHING] = value;
        }

        public bool IsParkourInput
        {
            get => this[eCharacterActionInputType.PARKOUR];
            set => this[eCharacterActionInputType.PARKOUR] = value;
        }
        
        public bool IsEmoteInput
        {
            get => this[eCharacterActionInputType.EMOTE];
            set => this[eCharacterActionInputType.EMOTE] = value;
        }
        
        public bool IsCinematicInput
        {
            get => this[eCharacterActionInputType.CINEMATIC];
            set => this[eCharacterActionInputType.CINEMATIC] = value;
        }
    }
}
