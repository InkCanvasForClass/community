using Ink_Canvas.Helpers;
using System;
using System.Windows.Controls;
using System.Windows.Ink;

namespace Ink_Canvas.Controls.Toolbar.BoardToolbar
{
    public class WhiteboardPageManager
    {
        private const int MaxPages = 99;
        private const int MaxPageSlots = 101;

        private readonly StrokeCollection[] _strokeCollections = new StrokeCollection[MaxPageSlots];
        private readonly bool[] _whiteboardLastModeIsRedo = new bool[MaxPageSlots];
        private readonly StrokeCollection _lastTouchDownStrokeCollection = new StrokeCollection();
        private readonly TimeMachineHistory[][] _timeMachineHistories = new TimeMachineHistory[MaxPageSlots][];
        private readonly bool[] _savedMultiTouchModeStates = new bool[MaxPageSlots];

        private int _currentIndex = 1;
        private int _totalCount = 1;

        public int CurrentIndex => _currentIndex;
        public int TotalCount => _totalCount;
        public bool CanGoToPrevious => _currentIndex > 1;
        public bool CanGoToNext => _currentIndex < _totalCount;
        public bool CanAddNewPage => _totalCount < MaxPages;
        public bool CanDeletePage => _totalCount > 1;

        public string PageInfo => $"{_currentIndex}/{_totalCount}";

        public event Action PageChanged;

        public TimeMachineHistory[] GetCurrentPageHistory()
        {
            return _timeMachineHistories[_currentIndex];
        }

        public void SetCurrentPageHistory(TimeMachineHistory[] history)
        {
            _timeMachineHistories[_currentIndex] = history;
        }

        public bool GetCurrentPageMultiTouchMode()
        {
            return _savedMultiTouchModeStates[_currentIndex];
        }

        public void SetCurrentPageMultiTouchMode(bool isInMultiTouchMode)
        {
            _savedMultiTouchModeStates[_currentIndex] = isInMultiTouchMode;
        }

        public bool GoToPreviousPage()
        {
            if (!CanGoToPrevious) return false;
            _currentIndex--;
            PageChanged?.Invoke();
            return true;
        }

        public bool GoToNextPage()
        {
            if (!CanGoToNext) return false;
            _currentIndex++;
            PageChanged?.Invoke();
            return true;
        }

        public bool AddNewPage()
        {
            if (!CanAddNewPage) return false;

            _totalCount++;
            _currentIndex++;

            if (_currentIndex != _totalCount)
            {
                for (var i = _totalCount; i > _currentIndex; i--)
                {
                    _timeMachineHistories[i] = _timeMachineHistories[i - 1];
                    _savedMultiTouchModeStates[i] = _savedMultiTouchModeStates[i - 1];
                }
            }

            _timeMachineHistories[_currentIndex] = null;
            PageChanged?.Invoke();
            return true;
        }

        public bool DeletePage(int pageIndex)
        {
            if (_totalCount <= 1 || pageIndex < 1 || pageIndex > _totalCount)
                return false;

            if (pageIndex == _currentIndex)
            {
                var oldTotal = _totalCount;
                if (_currentIndex != oldTotal)
                {
                    for (var i = _currentIndex; i < oldTotal; i++)
                    {
                        _timeMachineHistories[i] = _timeMachineHistories[i + 1];
                        _savedMultiTouchModeStates[i] = _savedMultiTouchModeStates[i + 1];
                    }
                }
                else
                {
                    _currentIndex--;
                }

                _timeMachineHistories[oldTotal] = null;
                _totalCount--;
            }
            else if (pageIndex < _currentIndex)
            {
                for (var i = pageIndex; i < _totalCount; i++)
                {
                    _timeMachineHistories[i] = _timeMachineHistories[i + 1];
                    _savedMultiTouchModeStates[i] = _savedMultiTouchModeStates[i + 1];
                }
                _timeMachineHistories[_totalCount] = null;
                _totalCount--;
                _currentIndex--;
            }
            else
            {
                for (var i = pageIndex; i < _totalCount; i++)
                {
                    _timeMachineHistories[i] = _timeMachineHistories[i + 1];
                    _savedMultiTouchModeStates[i] = _savedMultiTouchModeStates[i + 1];
                }
                _timeMachineHistories[_totalCount] = null;
                _totalCount--;
            }

            PageChanged?.Invoke();
            return true;
        }

        public void UpdatePageInfoDisplay(TextBlock pageInfoTextBlock)
        {
            if (pageInfoTextBlock != null)
            {
                pageInfoTextBlock.Text = PageInfo;
            }
        }

        public void Reset()
        {
            for (int i = 0; i < MaxPageSlots; i++)
            {
                _strokeCollections[i] = null;
                _timeMachineHistories[i] = null;
                _savedMultiTouchModeStates[i] = false;
                _whiteboardLastModeIsRedo[i] = false;
            }
            _currentIndex = 1;
            _totalCount = 1;
        }
    }
}
