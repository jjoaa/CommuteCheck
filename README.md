# 근태관리 키오스크
<br />

## 1. 소개
> 키오스크 환경에서 사용자 인증 및 출입 관리를 위한 통합 솔루션 입니다. <br />
> 비접촉식 출입 관리 시스템 구현으로 보안성 및 사용자 편의성을 제공합니다.
<br />
<img width="400" height="600" alt="Image" src="https://github.com/user-attachments/assets/296b394b-14c3-472f-97c8-2ad46121fe19" />

### 작업기간
2025/07-10, 3달
<br /><br />

### 인력구성
1인
<br /><br /><br />

## 2. 기술스택

<img src ="https://img.shields.io/badge/C_sharp-003545.svg?&style=for-the-badge&logo=Csharp&logoColor=brown"> <img src="https://img.shields.io/badge/c++-00599C?style=for-the-badge&logo=c%2B%2B&logoColor=white"> <img src="https://img.shields.io/badge/firebase-FFCA28?style=for-the-badge&logo=firebase&logoColor=white" > <img src="https://img.shields.io/badge/postgresql-4169E1?style=for-the-badge&logo=postgresql&logoColor=white"/>

<br /><br />

## 3. 기능 
- 얼굴 인식 기반 사용자 인증
- 비콘 자동 감지 및 인증
- 출퇴근 기록 확인
- 직원 확인 및 등록
<br /><br />

## 4. 📂 Project Structure (폴더 구조)
```
CommuteCheck/
├──extern/
│   └─ SDK               # 외부 SDK
├── src/   
    └─ SdkWrapper/       #  C++/CLI 래퍼
    └─ Kiosk/            #  C# WPF 앱
        ├── Commands/    # 커맨드 패턴 관련 클래스
        ├── Converters/  # UI 데이터 변환 로직
        ├── FaceEngine/  # 얼굴 인식 관련 핵심 로직
        ├── Models/      # 데이터 모델
        ├── Pages/       # UI 페이지 정의
        ├── Services/    # 비즈니스 로직 및 외부 서비스 통합
        ├── Utils/       # 유틸리티 및 헬퍼 클래스
        ├── ViewModels/  # MVVM 패턴의 뷰모델
        ├── Views/       # 추가 뷰 컴포넌트
        ├── App.xaml
        ├── App.xaml.cs
        ├── MainWindow.xaml
        ├── MainWindow.xaml.cs      

```
<br /><br />

## 5. 상세 이미지
<img width="400" height="600" alt="Image" src="https://github.com/user-attachments/assets/d8186518-4982-466d-b140-f1ec9abd7ac2" />
<img width="400" height="600" alt="Image" src="https://github.com/user-attachments/assets/dba5ce25-8ec6-4b09-9210-8966161a963c" /> <br/>
<img width="400" height="600" alt="Image" src="https://github.com/user-attachments/assets/2bdda496-cecb-4826-af5e-91240888385a" /> 
<img width="400" height="800" alt="Image" src="https://github.com/user-attachments/assets/c8e6df87-44d2-442e-b6de-6ece2adbc677" /> <br/>
<img width="400" height="600" alt="Image" src="https://github.com/user-attachments/assets/fc7f6252-25d7-468f-8fd2-f465d51e6ed6" /> 
<img width="400" height="600" alt="Image" src="https://github.com/user-attachments/assets/296b394b-14c3-472f-97c8-2ad46121fe19" />  <br/> <br/>
