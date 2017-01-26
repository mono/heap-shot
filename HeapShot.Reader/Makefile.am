NAME = HeapShot.Reader
TARGET = $(NAME).dll
CSPROJ = $(NAME).csproj
XBUILD := $(shell (xbuild /version &> /dev/null && echo xbuild) || (/Library/Frameworks/Mono.framework/Commands/xbuild /version &> /dev/null && echo /Library/Frameworks/Mono.framework/Commands/xbuild))
XBUILD_ARGS = "/property:Configuration=Debug" $(CSPROJ)

CSFILES = $(wildcard *.cs)

$(TARGET): $(CSFILES) $(CSPROJ)
	$(XBUILD) $(XBUILD_ARGS)

all: $(TARGET)

install-data-local: $(TARGET)
	$(mkinstalldirs) $(DESTDIR)$(pkglibdir)
	$(INSTALL_DATA) $(TARGET) $(DESTDIR)$(pkglibdir)

uninstall-local:
	cd $(DESTDIR)$(pkglibdir) && rm -f $(TARGET)

EXTRA_DIST = 			\
	$(CSFILES)

CLEANFILES =			\
	$(TARGET)		\
	$(TARGET).mdb

clean-local:
	$(XBUILD) $(XBUILD_ARGS) /t:Clean
	rm -rf bin obj

