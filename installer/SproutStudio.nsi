Unicode true
RequestExecutionLevel user
SetCompressor /SOLID lzma

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"

!ifndef VERSION
  !error "VERSION define is required"
!endif
!ifndef PUBLISH_DIR
  !error "PUBLISH_DIR define is required"
!endif
!ifndef OUTPUT_DIR
  !error "OUTPUT_DIR define is required"
!endif
!ifndef APP_ICON
  !error "APP_ICON define is required"
!endif
!ifndef LICENSE_FILE
  !error "LICENSE_FILE define is required"
!endif
!ifndef WELCOME_BITMAP
  !error "WELCOME_BITMAP define is required"
!endif

!define PRODUCT_NAME "Sprout Studio"
!define PRODUCT_PUBLISHER "Sprout Studio Contributors"
!define PRODUCT_KEY "Software\SproutStudio"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\SproutStudio"
!define SOURCE_CLASS "SproutStudio.Source"

Name "${PRODUCT_NAME} ${VERSION}"
OutFile "${OUTPUT_DIR}\SproutStudio-Setup-x64.exe"
InstallDir "$LOCALAPPDATA\Programs\Sprout Studio"
Icon "${APP_ICON}"
UninstallIcon "${APP_ICON}"
BrandingText "Sprout Studio"
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "${PRODUCT_NAME}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "${PRODUCT_PUBLISHER}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright © Sprout Studio contributors"
VIAddVersionKey /LANG=1033 "FileDescription" "Sprout Studio Setup"

!define MUI_ABORTWARNING
!define MUI_ICON "${APP_ICON}"
!define MUI_UNICON "${APP_ICON}"
!define MUI_WELCOMEFINISHPAGE_BITMAP "${WELCOME_BITMAP}"
!define MUI_FINISHPAGE_RUN "$INSTDIR\SproutStudio.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Launch Sprout Studio"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LICENSE_FILE}"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "Sprout Studio requires 64-bit Windows."
    Abort
  ${EndIf}
  SetShellVarContext current
  SetRegView 64
  ReadRegStr $0 HKCU "${PRODUCT_KEY}" "InstallLocation"
  ${If} $0 != ""
    StrCpy $INSTDIR $0
  ${EndIf}
FunctionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
FunctionEnd

Function RemoveLegacyRegistration
  ; Releases before 0.1.0 did not explicitly select the x64 registry view.
  ; Remove only Sprout-owned legacy keys before writing canonical x64 keys.
  SetRegView 32
  ReadRegStr $1 HKCU "Software\Classes\.spr" ""
  ${If} $1 == "${SOURCE_CLASS}"
    DeleteRegKey HKCU "Software\Classes\.spr"
  ${EndIf}
  DeleteRegKey HKCU "Software\Classes\${SOURCE_CLASS}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "${PRODUCT_KEY}"
  SetRegView 64
FunctionEnd

Function un.RemoveRegistrationForCurrentView
  ReadRegStr $1 HKCU "Software\Classes\.spr" ""
  ${If} $1 == "${SOURCE_CLASS}"
    DeleteRegKey HKCU "Software\Classes\.spr"
  ${EndIf}
  DeleteRegKey HKCU "Software\Classes\${SOURCE_CLASS}"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
  DeleteRegKey HKCU "${PRODUCT_KEY}"
FunctionEnd

Section "!Sprout Studio (required)" SecCore
  SectionIn RO

  ; Upgrade in place using the existing uninstaller. User settings, Academy
  ; progress, logs, and projects live outside this program directory.
  ${If} ${FileExists} "$INSTDIR\Uninstall.exe"
    ClearErrors
    ExecWait '"$INSTDIR\Uninstall.exe" /S _?=$INSTDIR' $0
    ${If} ${Errors}
      Abort "The previous Sprout Studio uninstaller could not be started."
    ${ElseIf} $0 != 0
      Abort "The previous Sprout Studio installation could not be removed (exit code $0)."
    ${EndIf}
  ${EndIf}
  Call RemoveLegacyRegistration

  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\Sprout Studio"
  CreateShortcut "$SMPROGRAMS\Sprout Studio\Sprout Studio.lnk" "$INSTDIR\SproutStudio.exe" "" "$INSTDIR\SproutStudio.exe" 0
  CreateShortcut "$SMPROGRAMS\Sprout Studio\Uninstall Sprout Studio.lnk" "$INSTDIR\Uninstall.exe"

  ClearErrors
  WriteRegStr HKCU "${PRODUCT_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${PRODUCT_KEY}" "Version" "${VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${PRODUCT_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "${PRODUCT_PUBLISHER}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\SproutStudio.exe,0"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" $0
  ${If} ${Errors}
    Abort "Sprout Studio files were installed, but Windows registration failed."
  ${EndIf}
  ReadRegStr $1 HKCU "${PRODUCT_KEY}" "Version"
  ${If} $1 != "${VERSION}"
    Abort "Sprout Studio could not verify its Windows registration."
  ${EndIf}
  ReadRegStr $1 HKCU "${UNINSTALL_KEY}" "DisplayVersion"
  ReadRegStr $2 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $1 != "${VERSION}"
  ${OrIf} $2 != "$INSTDIR"
    Abort "Sprout Studio could not verify its uninstall registration."
  ${EndIf}
SectionEnd

Section /o "Desktop shortcut" SecDesktop
  CreateShortcut "$DESKTOP\Sprout Studio.lnk" "$INSTDIR\SproutStudio.exe" "" "$INSTDIR\SproutStudio.exe" 0
SectionEnd

Section "Associate .spr source files" SecAssociation
  ClearErrors
  WriteRegStr HKCU "Software\Classes\.spr" "" "${SOURCE_CLASS}"
  WriteRegStr HKCU "Software\Classes\${SOURCE_CLASS}" "" "Sprout Source File"
  WriteRegStr HKCU "Software\Classes\${SOURCE_CLASS}\DefaultIcon" "" "$INSTDIR\SproutStudio.exe,0"
  WriteRegStr HKCU "Software\Classes\${SOURCE_CLASS}\shell\open\command" "" '"$INSTDIR\SproutStudio.exe" "%1"'
  ${If} ${Errors}
    Abort "Sprout Studio could not register the .spr file association."
  ${EndIf}
  ReadRegStr $0 HKCU "Software\Classes\.spr" ""
  ${If} $0 != "${SOURCE_CLASS}"
    Abort "Sprout Studio could not verify the .spr file association."
  ${EndIf}
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
SectionEnd

LangString DESC_SecCore ${LANG_ENGLISH} "The self-contained Sprout Studio IDE, Rust engine, Sprout CLI, offline documentation, Academy content, and bundled Python runtime."
LangString DESC_SecDesktop ${LANG_ENGLISH} "Add an optional Sprout Studio shortcut to your desktop."
LangString DESC_SecAssociation ${LANG_ENGLISH} "Open .spr source files with Sprout Studio from Windows Explorer."

!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
  !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} $(DESC_SecCore)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecDesktop} $(DESC_SecDesktop)
  !insertmacro MUI_DESCRIPTION_TEXT ${SecAssociation} $(DESC_SecAssociation)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

Section "Uninstall"
  Delete "$DESKTOP\Sprout Studio.lnk"
  Delete "$SMPROGRAMS\Sprout Studio\Sprout Studio.lnk"
  Delete "$SMPROGRAMS\Sprout Studio\Uninstall Sprout Studio.lnk"
  RMDir "$SMPROGRAMS\Sprout Studio"

  SetRegView 64
  Call un.RemoveRegistrationForCurrentView
  SetRegView 32
  Call un.RemoveRegistrationForCurrentView
  SetRegView 64
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'

  ; Only the exact application installation directory is removed. Mutable
  ; data under LocalAppData\SproutStudio and user-created projects remain.
  RMDir /r "$INSTDIR"
SectionEnd
