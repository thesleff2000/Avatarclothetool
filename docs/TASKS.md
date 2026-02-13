# Implementation Tasks (MVP)

## TICKET-01: Closet Config Data Model + Serialization

- Goal
  - Outfit 등록/생성을 위한 최소 데이터 모델과 저장 경로를 확정한다.
- Input
  - Avatar root reference
  - Outfit `GameObject` reference(s)
  - 기본 생성 설정(prefix/menu label 등)
- Output
  - `ClosetConfig` 저장 자산(또는 tool-owned component/asset)
  - `OutfitEntry`(stable ID 포함) 직렬화
- Expected Files To Modify
  - `Packages/com.yourname.avatar-closet-tool/Runtime/` (config/data classes)
  - `Packages/com.yourname.avatar-closet-tool/Editor/` (asset create/load utilities)
  - `README.md` (config 개요 업데이트 가능)
- Done Criteria (Validation Steps)
  - Unity 컴파일 에러 0
  - config 생성/저장/재열기 시 데이터 유지
  - 동일 outfit 재등록 시 중복/ID 규칙이 의도대로 동작

## TICKET-02: Outfit Toggle Registration UI/Workflow

- Goal
  - 사용자가 outfit `GameObject`를 지정하고 토글 항목을 등록/관리할 수 있게 한다.
- Input
  - 사용자 선택 `GameObject`
  - 표시명, default on/off
- Output
  - 등록/수정/삭제 가능한 outfit 목록
  - 유효성 검사 결과(누락/중복/잘못된 계층)
- Expected Files To Modify
  - `Packages/com.yourname.avatar-closet-tool/Editor/` (menu/window/inspector tools)
  - `Packages/com.yourname.avatar-closet-tool/Runtime/` (entry validation helpers)
  - `AGENTS.md` (필요 시 검증 체크리스트 보강)
- Done Criteria (Validation Steps)
  - Unity 컴파일 에러 0
  - 등록/수정/삭제가 저장 후 재시작해도 유지
  - 잘못된 입력에서 경고/차단 동작 확인

## TICKET-03: MA-Based Menu/Parameter Generator

- Goal
  - 등록된 outfit 목록으로 MA 기반 메뉴/파라미터 모듈을 자동 생성한다.
- Input
  - `ClosetConfig` + outfit entries
  - 생성 옵션(prefix/root label)
- Output
  - tool-owned MA 모듈(메뉴/파라미터 대응 구성)
  - 생성 로그(생성/업데이트 대상)
- Expected Files To Modify
  - `Packages/com.yourname.avatar-closet-tool/Editor/` (generator entrypoint/commands)
  - `Packages/com.yourname.avatar-closet-tool/Runtime/` (generation model)
  - `README.md` (생성 사용법)
- Done Criteria (Validation Steps)
  - Unity 컴파일 에러 0
  - 메뉴/파라미터가 등록 수량과 일치
  - 원본 FX/Menu/Params 자산 직접 변경이 발생하지 않음

## TICKET-04: Drift Detection + Repair/Rebuild Engine

- Goal
  - 렌더러/머티리얼/텍스처 변경으로 인한 깨짐을 감지하고 재스캔 후 재생성한다.
- Input
  - 기존 `ClosetConfig`
  - 현재 avatar hierarchy + renderer/material 상태
- Output
  - 깨짐 감지 결과(원인 코드/메시지)
  - 재생성된 MA 모듈 + 복구 리포트
- Expected Files To Modify
  - `Packages/com.yourname.avatar-closet-tool/Runtime/` (fingerprint/diff logic)
  - `Packages/com.yourname.avatar-closet-tool/Editor/` (repair command/report UI)
  - `docs/MVP.md` (복구 시나리오 정밀화 가능)
- Done Criteria (Validation Steps)
  - Unity 컴파일 에러 0
  - 의도적으로 renderer/material 변경 후 drift 감지됨
  - Repair/Rebuild 1회 실행으로 모듈 재구성 및 보고서 생성

## TICKET-05: End-to-End Verification + Docs Hardening

- Goal
  - MVP 3기능의 연결 동작을 검증하고 문서를 개발/운영 기준으로 고정한다.
- Input
  - 샘플 avatar + 최소 2개 outfit
  - 기존 tickets 1~4 산출물
- Output
  - E2E 검증 체크리스트 통과 결과
  - 문서 최신화(`README`, `AGENTS`, `docs/MVP`)
- Expected Files To Modify
  - `README.md`
  - `AGENTS.md`
  - `docs/MVP.md`
  - `docs/TASKS.md`
- Done Criteria (Validation Steps)
  - Unity 컴파일 에러 0
  - 등록 -> 생성 -> drift 유도 -> repair/rebuild 플로우 성공
  - 신규 개발자가 문서만 읽고 동일 절차를 재현 가능
