using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Diplom_1.Models;
using Diplom_1.Services;

namespace Diplom_1.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppSettings _settings;

        private bool _isOllamaMode = true;
        private bool _isApiMode;
        private string _ollamaUrl = "http://localhost:11434";
        private string _ollamaModel = "llama3";
        private string _apiUrl = string.Empty;
        private string _apiKey = string.Empty;
        private string _apiModel = "gpt-3.5-turbo";
        private string _connectionStatus = string.Empty;
        private bool _isTesting;
        private bool _isDebugMode;

        public bool IsOllamaMode
        {
            get => _isOllamaMode;
            set
            {
                if (SetField(ref _isOllamaMode, value) && value)
                {
                    IsApiMode = false;
                }
            }
        }

        public bool IsApiMode
        {
            get => _isApiMode;
            set
            {
                if (SetField(ref _isApiMode, value) && value)
                {
                    IsOllamaMode = false;
                }
            }
        }

        public string OllamaUrl
        {
            get => _ollamaUrl;
            set => SetField(ref _ollamaUrl, value);
        }

        public string OllamaModel
        {
            get => _ollamaModel;
            set => SetField(ref _ollamaModel, value);
        }

        public string ApiUrl
        {
            get => _apiUrl;
            set => SetField(ref _apiUrl, value);
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetField(ref _apiKey, value);
        }

        public string ApiModel
        {
            get => _apiModel;
            set => SetField(ref _apiModel, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetField(ref _connectionStatus, value);
        }

        public bool IsTesting
        {
            get => _isTesting;
            set => SetField(ref _isTesting, value);
        }

        public bool IsDebugMode
        {
            get => _isDebugMode;
            set => SetField(ref _isDebugMode, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand CancelCommand { get; }

        public SettingsViewModel(AppSettings settings)
        {
            _settings = settings;

            IsOllamaMode = settings.AiMode == AiMode.Ollama;
            IsApiMode = settings.AiMode == AiMode.Api;
            OllamaUrl = settings.OllamaUrl;
            OllamaModel = settings.OllamaModel;
            ApiUrl = settings.ApiUrl;
            ApiKey = settings.ApiKey;
            ApiModel = settings.ApiModel;
            IsDebugMode = settings.DebugMode;

            SaveCommand = new RelayCommand(Save);
            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync());
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Save(object? parameter)
        {
            _settings.AiMode = IsOllamaMode ? AiMode.Ollama : AiMode.Api;
            _settings.OllamaUrl = OllamaUrl;
            _settings.OllamaModel = OllamaModel;
            _settings.ApiUrl = ApiUrl;
            _settings.ApiKey = ApiKey;
            _settings.ApiModel = ApiModel;
            _settings.DebugMode = IsDebugMode;
            _settings.Save();

            ConnectionStatus = "Настройки сохранены";
            MessageBox.Show("Настройки сохранены.", "Сохранение",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task TestConnectionAsync()
        {
            var tempSettings = new AppSettings
            {
                AiMode = IsOllamaMode ? AiMode.Ollama : AiMode.Api,
                OllamaUrl = OllamaUrl,
                OllamaModel = OllamaModel,
                ApiUrl = ApiUrl,
                ApiKey = ApiKey
            };

            var aiService = new AiService(tempSettings);

            IsTesting = true;
            ConnectionStatus = "Проверка подключения...";

            var result = await aiService.TestConnectionAsync();

            IsTesting = false;

            if (result)
            {
                ConnectionStatus = "Подключение успешно";
                MessageBox.Show("Подключение к AI успешно установлено.", "Проверка подключения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                ConnectionStatus = "Ошибка подключения";
                MessageBox.Show("Не удалось подключиться к AI. Проверьте настройки.", "Ошибка подключения",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel(object? parameter)
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window.DataContext == this)
                {
                    window.Close();
                    return;
                }
            }
        }
    }
}
