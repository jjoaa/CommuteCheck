using Kiosk.Services.Interface;
using Kiosk.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Kiosk.Pages
{
    /// <summary>
    /// CheckPhonePage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CheckPhonePage : Page
    {
        private readonly INavigationService _navigationService;
        private readonly CheckPhoneViewModel _viewModel;

        public CheckPhonePage(
            INavigationService navigationService, 
            CheckPhoneViewModel viewModel)
        {
            InitializeComponent();
            _navigationService = navigationService;
            _viewModel = viewModel;
            DataContext = _viewModel;

            Dispatcher.InvokeAsync(AddKeypadEventHandlers, DispatcherPriority.Loaded);
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            _navigationService.NavigateTo<FaceRecognitionViewModel>();
        }

        private void AddKeypadEventHandlers()
        {
            Debug.WriteLine("AddKeypadEventHandlers called.");
            // 키패드의 모든 버튼에 이벤트 핸들러 추가
            foreach (Button button in FindVisualChildren<Button>(this))
            {
                if (button.Content is string content)
                {
                    Debug.WriteLine($"Found button with string content: {content}");
                    if (IsNumberButton(content))
                    {
                        button.Click += NumberButton_Click;
                        Debug.WriteLine($"  Assigned NumberButton_Click to: {content}");
                    }
                    else if (content == "⌫")
                    {
                        button.Click += BackspaceButton_Click;
                        Debug.WriteLine($"  Assigned BackspaceButton_Click (text) to: {content}");
                    }
                    else if (content.Contains("전체지움"))
                    {
                        button.Click += ClearButton_Click;
                        Debug.WriteLine($"  Assigned ClearButton_Click to: {content}");
                    }
                }
                // 이미지가 Content인 버튼 처리 (백스페이스 버튼)
                else if (button.Content is Image img)
                {
                    Debug.WriteLine($"Found button with image content. Source: {img.Source?.ToString()}");
                    if (img.Source != null && img.Source.ToString().Contains("btnRemove"))
                    {
                        button.Click += BackspaceButton_Click;
                        Debug.WriteLine($"  Assigned BackspaceButton_Click (image) to: btnRemove");
                    }
                }
                else
                {
                    Debug.WriteLine($"Found button with unhandled content type: {button.Content?.GetType().Name ?? "null"}");
                }
            }
            Debug.WriteLine("Finished AddKeypadEventHandlers.");
        }

        private bool IsNumberButton(string content)
        {
            return content == "1" || content == "2" || content == "3" || content == "4" || content == "5" ||
                   content == "6" || content == "7" || content == "8" || content == "9" || content == "0";
        }

        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            /*Button button = sender as Button;
            if (button?.Content is string number)
            {
                Debug.WriteLine($"Number button clicked: {number}");
                if (string.IsNullOrEmpty(_viewModel.PhoneNumber) || _viewModel.PhoneNumber.Length < 11)
                {
                    _viewModel.PhoneNumber += number;
                    UpdatePhoneNumberDisplay();
                    Debug.WriteLine($"PhoneNumber updated to: {_viewModel.PhoneNumber}");
                }
            }*/
            /*if (sender is Button { Content: string number } && DataContext is CheckPhoneViewModel vm)
            {
                // 숫자만 11자리까지
                var digits = new string((vm.PhoneNumber + number).Where(char.IsDigit).ToArray());
                if (digits.Length <= 11)
                    vm.PhoneNumber = digits; // ← 화면은 바인딩으로 자동 갱신
            }*/
            if (DataContext is CheckPhoneViewModel vm && sender is Button { Content: string number })
            {
                var digits = new string(((vm.PhoneNumber ?? "") + number).Where(char.IsDigit).ToArray());
                if (digits.Length <= 11) vm.PhoneNumber = digits; // 화면은 바인딩으로 자동
            }
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Backspace button clicked.");
            /*if (DataContext is CheckPhoneViewModel vm && !string.IsNullOrEmpty(vm.PhoneNumber))
            {
                var digits = vm.PhoneNumber;
                vm.PhoneNumber = digits[..^1];
            }*/
            if (DataContext is CheckPhoneViewModel vm && !string.IsNullOrEmpty(vm.PhoneNumber))
                vm.PhoneNumber = vm.PhoneNumber[..^1];
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("Clear all button clicked.");
            if (DataContext is CheckPhoneViewModel vm)
                vm.PhoneNumber = "";
        }

        /*private void UpdatePhoneNumberDisplay()
        {
            var digits = new string(_viewModel.PhoneNumber.Where(char.IsDigit).ToArray());

            if (digits.Length == 0)
            {
                PhoneNumberDisplay.Text = "010";
                return;
            }

            if (digits.Length <= 3)
            {
                PhoneNumberDisplay.Text = digits;
            }
            else if (digits.Length <= 7)
            {
                PhoneNumberDisplay.Text = $"{digits.Substring(0, 3)}-{digits.Substring(3)}";
            }
            else
            {
                PhoneNumberDisplay.Text = $"{digits.Substring(0, 3)}-{digits.Substring(3, 4)}-{digits.Substring(7)}";
            }
            Debug.WriteLine($"PhoneNumberDisplay.Text set to: {PhoneNumberDisplay.Text}");
        }*/

        // Visual Tree에서 특정 타입의 자식 요소들을 찾는 헬퍼 메서드
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null)
                yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T)
                {
                    yield return (T)child;
                }

                foreach (T childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }
    }
}
