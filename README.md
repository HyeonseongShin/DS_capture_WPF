# DS Capture (WPF)

Python/tkinter 버전을 C# + WPF (.NET 8)으로 포팅한 Windows 화면 캡처 및 이미지 편집 도구입니다.  
HWID 기반 라이센스 인증을 통해 인가된 사용자만 사용할 수 있습니다.

---

## 주요 기능

### 캡처 모드

| 모드 | 설명 |
|------|------|
| **지정크기 캡처** | 미리 설정한 픽셀 크기의 투명 박스를 화면에 배치하고 `Enter` 또는 CAPTURE 버튼으로 캡처 |
| **자유 드래그 캡처** | 전체화면 어두운 오버레이에서 마우스 드래그로 영역 선택. `Shift` 비율 고정, `Ctrl` 중앙 기준 드래그 |
| **전체 화면 캡처** | 전체 화면을 즉시 캡처 |

캡처 즉시 **클립보드에 자동 복사**되며 지정 폴더에 파일로 저장됩니다.

---

### 이미지 편집기

캡처 후 우측 패널 썸네일을 **더블클릭**하면 편집기가 열립니다.

**그리기 도구** (GDI+ 기반)
- 펜, 직선, 화살표, 사각형, 원, 텍스트
- 형광펜 (반투명 알파 블렌딩)
- 모자이크 (선택 영역 픽셀화)
- 자르기 (이미지 크롭)

**편집 옵션**
- 선 색상 / 채우기 색상 (팔레트 8색 + 사용자 지정)
- 선 두께, 채우기 켬/끔
- 글꼴 및 크기 선택

**변환 및 저장**
- 90° 회전, 좌우/상하 반전
- 실행취소 `Ctrl+Z` / 재실행 `Ctrl+Y` (최대 10단계)
- 저장, 다른 이름으로 저장 (PNG/JPG), 클립보드 복사

---

### 환경설정

- **단축키 지정**: 각 캡처 모드에 전역 단축키 (`Ctrl+Alt+Shift+키` 조합) 설정
- **저장 위치**: 캡처 이미지 저장 폴더 지정
- **저장 형식**: PNG 또는 JPG
- **닫기 동작**: 트레이로 최소화 또는 완전 종료
- **시작 프로그램**: Windows 로그인 시 자동 실행 (`--startup` 모드로 트레이에 숨겨서 시작)

---

### 시스템 트레이

창을 닫으면 시스템 트레이에 아이콘이 표시됩니다.  
우클릭 메뉴: 열기 / 폴더 열기 / 종료

---

### 최근 캡처 목록

우측 패널에 최근 캡처 이미지를 최대 10개 썸네일로 표시합니다.

- **더블클릭**: 편집기 열기
- **우클릭**: 이미지 수정 / 다른 이름으로 저장 / 삭제
- **모두 지우기**: 목록 및 실제 파일 삭제
- **다른 폴더에 모두 저장**: 선택 폴더에 일괄 복사

---

## 라이센스 인증

프로그램 실행 시 `.lic` 파일로 HWID 인증을 수행합니다.

1. `C:\license\` 폴더 또는 실행 파일 폴더에 `.lic` 파일을 배치합니다.
2. 인증 실패 시 오류 창에 **기기 고유 ID(HWID)** 가 표시됩니다.
3. HWID를 복사하여 관리자에게 전달하면 라이센스 파일을 발급받을 수 있습니다.

**HWID 생성 방식**: WMI로 마더보드 + 디스크 시리얼 조합 → SHA-256 해시 → `XXXX-XXXX-XXXX` 형식  
**서명 방식**: HMAC-SHA256 (Python 버전과 동일한 키/알고리즘 — 기존 `.lic` 파일 재사용 가능)

라이센스 파일 형식 (`.lic`, UTF-8 JSON):
```json
{
  "hwid": "ABCD-1234-EF56",
  "app_name": "DS_CAPTURE",
  "user_name": "홍길동",
  "expiry_date": "2027-12-31",
  "signature": "<hmac-sha256-hex>"
}
```
`expiry_date`는 `"PERMANENT"` 또는 `"YYYY-MM-DD"` 지원.

---

## 프로젝트 구조

```
DSCapture_WPF/
├── DSCapture.sln
├── build.ps1                        ← 빌드 스크립트 (버전 자동 증가 + dotnet publish)
└── DSCapture/
    ├── DSCapture.csproj             ← .NET 8 WPF, System.Drawing.Common, System.Management
    ├── App.xaml / App.xaml.cs       ← 진입점: 단일 인스턴스, 라이센스 체크
    ├── Models/
    │   ├── AppSettings.cs           ← settings.json 스키마
    │   └── LicenseData.cs           ← .lic 파일 스키마
    ├── Helpers/
    │   ├── NativeMethods.cs         ← Win32 P/Invoke (RegisterHotKey, EnumWindows 등)
    │   └── BitmapHelper.cs          ← GDI+ Bitmap ↔ WPF BitmapSource 변환
    ├── Services/
    │   ├── HwidService.cs           ← HWID 생성 (WMI + SHA-256)
    │   ├── LicenseService.cs        ← 라이센스 검증 (HMAC-SHA256)
    │   ├── CaptureService.cs        ← 화면 캡처 + 클립보드 + 파일 저장
    │   ├── SettingsService.cs       ← settings.json 읽기/쓰기
    │   ├── HotkeyService.cs         ← 전역 단축키 (RegisterHotKey + HwndSource hook)
    │   └── TrayService.cs           ← 시스템 트레이 (NotifyIcon)
    ├── ViewModels/
    │   ├── ViewModelBase.cs         ← INotifyPropertyChanged 기반 클래스
    │   └── RelayCommand.cs          ← ICommand 구현
    └── Views/
        ├── MainWindow.xaml/.cs      ← 메인 UI (캡처 버튼, 썸네일 패널)
        ├── FixedBoxWindow.xaml/.cs  ← 지정크기 투명 리사이징 박스
        ├── DragCaptureWindow.xaml/.cs ← 자유 드래그 전체화면 오버레이
        ├── ImageEditorWindow.xaml/.cs ← 이미지 편집기 (GDI+ 드로잉)
        ├── LicenseErrorWindow.xaml/.cs ← 라이센스 오류 팝업
        ├── SettingsWindow.xaml/.cs  ← 환경설정 팝업
        ├── ShortcutSettingsWindow.xaml/.cs ← 단축키 설정 팝업
        └── TextInputDialog.xaml/.cs ← 텍스트 입력 팝업 (편집기용)
```

---

## 빌드

### 요구사항

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 또는 `dotnet` CLI

### 빌드 방법

**Visual Studio**
1. `DSCapture.sln` 더블클릭
2. `Resources\icon.ico` 배치 (Python 프로젝트의 `icon.ico` 복사)
3. `F5` (Debug) 또는 `Ctrl+Shift+B` (Release 빌드)

**PowerShell (배포용 단일 EXE)**
```powershell
cd DSCapture_WPF
.\build.ps1
# → dist_production\DS Capture.exe 생성
```

버전 증가 없이 빌드:
```powershell
.\build.ps1 -NoVersion
```

### NuGet 패키지

| 패키지 | 용도 |
|--------|------|
| `System.Drawing.Common` | GDI+ 이미지 처리 (캡처, 편집기 드로잉) |
| `System.Management` | WMI 쿼리 (HWID용 마더보드/디스크 시리얼) |

---

## Python 버전과의 차이점

| 항목 | Python (구버전) | C# WPF (현재) |
|------|----------------|---------------|
| UI 프레임워크 | tkinter | WPF (XAML) |
| 이미지 처리 | PIL/Pillow | GDI+ (System.Drawing) |
| 전역 단축키 | `keyboard` 라이브러리 | `RegisterHotKey` Win32 API |
| 시스템 트레이 | `pystray` 라이브러리 | `NotifyIcon` (WinForms) |
| 클립보드 | ctypes (user32/kernel32 직접 호출) | `Clipboard.SetImage()` |
| 배포 | Nuitka onefile | `dotnet publish --single-file` |
| 라이센스 서명 | Python `hmac` + `hashlib.sha256` | `HMACSHA256` — **동일 알고리즘, 기존 `.lic` 파일 호환** |

---

## 시스템 요구사항

- Windows 10 / 11 (x64)
- .NET 8 Runtime (단일 파일 배포 시 불필요 — 런타임 포함)
- 라이센스 파일 (`.lic`)
