# GIF Creator

윈도우 캡처 도구처럼 **드래그로 화면 영역을 지정**해서 GIF로 녹화하는 초경량 Windows 프로그램입니다.

- 최대 **30초**, 최대 **15fps** 녹화
- 외부 라이브러리·런타임 설치 불필요 — Windows 10/11에 기본 내장된 .NET Framework만 사용
- 단일 exe (약 25KB)

- <img width="365" height="261" alt="image" src="https://github.com/user-attachments/assets/570c20a2-b2f9-4e41-a3a8-b9b1839ec3d7" />


## 사용법

1. `dist\GifCreator.exe` 실행
2. FPS(1~15)와 최대 길이(1~30초) 설정
3. **[● 영역 선택 후 녹화 시작]** 클릭
4. 화면이 어두워지면 캡처 도구처럼 **드래그로 영역 지정** (ESC로 취소)
5. 빨간 테두리가 표시되며 녹화 시작
6. **F9** 또는 **[■ 녹화 중지]** 버튼으로 중지 (최대 길이 도달 시 자동 중지)
7. 저장 위치를 지정하면 GIF 인코딩 후 저장 완료

옵션:
- **마우스 커서 포함** — 녹화 화면에 마우스 커서를 함께 그립니다 (기본 켜짐)
- **녹화 중 이 창 숨기기** — 녹화 중 메인 창을 숨깁니다 (F9로 중지)

## 빌드

Windows에 기본 내장된 C# 컴파일러(csc.exe)를 사용하므로 **별도 SDK 설치가 필요 없습니다.**

```bat
build.bat
```

결과물: `dist\GifCreator.exe`

## 테스트

인코딩 파이프라인 자체 검증 (프레임 수·딜레이·무한반복·화면캡처):

```bat
dist\GifCreator.exe --selftest
```

종료 코드 0 = 통과. 상세 결과는 `dist\selftest.log`에 기록됩니다.

## 구조

```
src\GifCreator.cs   전체 소스 (UI, 영역 선택 오버레이, 캡처 스레드, GIF 인코더)
src\app.manifest    DPI(디스플레이 배율) 대응 매니페스트 (PerMonitorV2)
build.bat           빌드 스크립트
dist\GifCreator.exe 빌드 결과물
```

## 동작 원리

- **영역 선택**: 전체 화면(멀티 모니터 포함)을 정지 화면으로 캡처한 뒤 어둡게 깔고, 드래그한 영역만 원본 밝기로 표시 — 윈도우 캡처 도구와 같은 방식
- **캡처**: 백그라운드 스레드에서 `Graphics.CopyFromScreen`으로 지정 fps에 맞춰 프레임을 캡처하고 PNG로 압축해 메모리에 보관. 각 프레임의 실제 타임스탬프를 기록해 시스템이 느려 프레임이 밀려도 재생 속도가 실제와 일치
- **GIF 인코딩**: Windows 내장 WIC(`GifBitmapEncoder`)로 인코딩한 뒤, GIF 바이트 스트림을 직접 순회하며 프레임별 딜레이(Graphic Control Extension)와 무한 반복(NETSCAPE2.0 확장)을 삽입 — WIC 인코더가 딜레이를 기록하지 않는 한계를 후처리로 해결
- **DPI**: PerMonitorV2 선언으로 디스플레이 배율(125%/150% 등) 환경에서도 물리 픽셀 좌표로 정확히 캡처

## 요구 사항

- Windows 10 / 11 (.NET Framework 4.x 내장)
